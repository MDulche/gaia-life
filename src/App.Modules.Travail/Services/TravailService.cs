using App.Modules.Travail.Data;
using App.Modules.Travail.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Travail.Services;

/// <summary>
/// Accès métier Travail. Un contexte par opération via factory.
/// Les données sont celles du foyer (pas de filtre par UserId).
/// </summary>
public sealed class TravailService
{
    /// <summary>Nom du rôle Identity autorisé à valider / refuser un congé.</summary>
    public const string RoleAdmin = "Admin";

    private readonly IDbContextFactory<TravailDbContext> _dbFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public TravailService(
        IDbContextFactory<TravailDbContext> dbFactory,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _dbFactory = dbFactory;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<List<Employeur>> ListerEmployeursAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Employeurs.AsNoTracking()
            .OrderBy(e => e.DateFin.HasValue)
            .ThenByDescending(e => e.DateDebut)
            .ToListAsync(cancellationToken);
    }

    public async Task<Employeur?> GetEmployeurAsync(int employeurId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Employeurs.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeurId, cancellationToken);
    }

    /// <summary>Emploi en cours le plus récent (<c>DateFin</c> nulle), sinon le plus récent tout court.</summary>
    public async Task<Employeur?> GetEmployeurActifAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var enCours = await db.Employeurs.AsNoTracking()
            .Where(e => e.DateFin == null)
            .OrderByDescending(e => e.DateDebut)
            .FirstOrDefaultAsync(cancellationToken);

        if (enCours is not null)
        {
            return enCours;
        }

        return await db.Employeurs.AsNoTracking()
            .OrderByDescending(e => e.DateDebut)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Employeur> AjouterEmployeurAsync(
        string nom,
        string? adresse,
        DateTime dateDebut,
        DateTime? dateFin,
        CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom de l'employeur est obligatoire.");
        }

        if (dateFin is not null && dateFin.Value.Date < dateDebut.Date)
        {
            throw new InvalidOperationException("La date de fin doit être postérieure ou égale à la date de début.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var employeur = new Employeur
        {
            Nom = trimmed,
            Adresse = string.IsNullOrWhiteSpace(adresse) ? null : adresse.Trim(),
            DateDebut = dateDebut.Date,
            DateFin = dateFin?.Date
        };

        db.Employeurs.Add(employeur);
        await db.SaveChangesAsync(cancellationToken);
        return employeur;
    }

    public async Task<List<FichePaie>> ListerFichesPaie(int employeurId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FichePaies.AsNoTracking()
            .Where(f => f.EmployeurId == employeurId)
            .OrderByDescending(f => f.Mois)
            .ToListAsync(cancellationToken);
    }

    public async Task<FichePaie?> DerniereFichePaieAsync(int employeurId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FichePaies.AsNoTracking()
            .Where(f => f.EmployeurId == employeurId)
            .OrderByDescending(f => f.Mois)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Refuse si le net dépasse le brut, ou si une fiche existe déjà pour le même mois.</summary>
    public async Task AjouterFichePaie(FichePaie fiche, CancellationToken cancellationToken = default)
    {
        ValiderFichePaie(fiche);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEmployeurExistsAsync(db, fiche.EmployeurId, cancellationToken);

        var exists = await db.FichePaies.AnyAsync(
            f => f.EmployeurId == fiche.EmployeurId && f.Mois == fiche.Mois,
            cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Une fiche de paie existe déjà pour ce mois.");
        }

        db.FichePaies.Add(fiche);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Conge>> ListerConges(
        int employeurId,
        StatutConge? filtreStatut = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Conges.AsNoTracking().Where(c => c.EmployeurId == employeurId);
        if (filtreStatut is not null)
        {
            query = query.Where(c => c.Statut == filtreStatut);
        }

        return await query
            .OrderByDescending(c => c.DateDebut)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Crée une demande en <see cref="StatutConge.EnAttente"/>. Échoue si chevauchement avec un congé validé.</summary>
    public async Task DemanderConge(Conge conge, CancellationToken cancellationToken = default)
    {
        ValiderConge(conge);
        conge.Statut = StatutConge.EnAttente;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEmployeurExistsAsync(db, conge.EmployeurId, cancellationToken);
        await EnsurePasDeChevauchementAsync(db, conge, cancellationToken);

        db.Conges.Add(conge);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Réservé Admin. Un congé validé ne peut pas chevaucher un autre congé déjà validé.</summary>
    public async Task ChangerStatutConge(int congeId, StatutConge nouveauStatut, CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync();

        if (nouveauStatut is not StatutConge.Valide and not StatutConge.Refuse)
        {
            throw new InvalidOperationException("Le nouveau statut doit être Validé ou Refusé.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var conge = await db.Conges.FirstOrDefaultAsync(c => c.Id == congeId, cancellationToken)
            ?? throw new InvalidOperationException("Demande de congé introuvable.");

        if (conge.Statut != StatutConge.EnAttente)
        {
            throw new InvalidOperationException("Seule une demande en attente peut être validée ou refusée.");
        }

        if (nouveauStatut == StatutConge.Valide)
        {
            await EnsurePasDeChevauchementAsync(db, conge, cancellationToken);
        }

        conge.Statut = nouveauStatut;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Reste = jours acquis − somme des <see cref="Conge.NombreJours"/> validés dont <c>DateDebut</c> est dans l'année.</summary>
    public async Task<SoldeCongesInfo> SoldeCongesActuel(int employeurId, int annee, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEmployeurExistsAsync(db, employeurId, cancellationToken);

        var solde = await db.SoldeConges.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EmployeurId == employeurId && s.Annee == annee, cancellationToken);

        var joursAcquis = solde?.JoursAcquis ?? 0;
        var joursPris = await JoursPrisValidesAsync(db, employeurId, annee, cancellationToken);
        return new SoldeCongesInfo(annee, joursAcquis, joursPris, joursAcquis - joursPris);
    }

    /// <summary>Crée ou met à jour <see cref="SoldeConges.JoursAcquis"/> (rattrapage d'historique).</summary>
    public async Task EnregistrerJoursAcquisAsync(
        int employeurId,
        int annee,
        decimal joursAcquis,
        CancellationToken cancellationToken = default)
    {
        if (annee is < 1990 or > 2100)
        {
            throw new InvalidOperationException("L'année est invalide.");
        }

        if (joursAcquis < 0)
        {
            throw new InvalidOperationException("Le nombre de jours acquis ne peut pas être négatif.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEmployeurExistsAsync(db, employeurId, cancellationToken);

        var solde = await db.SoldeConges
            .FirstOrDefaultAsync(s => s.EmployeurId == employeurId && s.Annee == annee, cancellationToken);

        if (solde is null)
        {
            db.SoldeConges.Add(new SoldeConges
            {
                EmployeurId = employeurId,
                Annee = annee,
                JoursAcquis = decimal.Round(joursAcquis, 2)
            });
        }
        else
        {
            solde.JoursAcquis = decimal.Round(joursAcquis, 2);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierEmployeurAsync(
        int id,
        string nom,
        string? adresse,
        DateTime dateDebut,
        DateTime? dateFin,
        CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom de l'employeur est obligatoire.");
        }

        if (dateFin is not null && dateFin.Value.Date < dateDebut.Date)
        {
            throw new InvalidOperationException("La date de fin doit être postérieure ou égale à la date de début.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var employeur = await db.Employeurs.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Employeur introuvable.");

        employeur.Nom = trimmed;
        employeur.Adresse = string.IsNullOrWhiteSpace(adresse) ? null : adresse.Trim();
        employeur.DateDebut = dateDebut.Date;
        employeur.DateFin = dateFin?.Date;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerEmployeurAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var employeur = await db.Employeurs.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employeur is null)
        {
            return;
        }

        db.Employeurs.Remove(employeur);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TravailEmployeurSolde>> ListerSoldesEmployeursActifsAsync(
        CancellationToken cancellationToken = default)
    {
        var annee = DateTime.Today.Year;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var actifs = await db.Employeurs.AsNoTracking()
            .Where(e => e.DateFin == null)
            .OrderByDescending(e => e.DateDebut)
            .ToListAsync(cancellationToken);

        var rows = new List<TravailEmployeurSolde>(actifs.Count);
        var premier = true;
        foreach (var employeur in actifs)
        {
            var joursAcquis = await db.SoldeConges.AsNoTracking()
                .Where(s => s.EmployeurId == employeur.Id && s.Annee == annee)
                .Select(s => (decimal?)s.JoursAcquis)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;
            var joursPris = await JoursPrisValidesAsync(db, employeur.Id, annee, cancellationToken);
            rows.Add(new TravailEmployeurSolde(
                employeur.Id,
                employeur.Nom,
                joursAcquis - joursPris,
                premier));
            premier = false;
        }

        return rows;
    }

    /// <summary>
    /// Calcul provisoire : rythme de prise de l'année (jours validés / mois écoulés)
    /// projeté sur les mois restants jusqu'au 31 décembre, soustrait du solde actuel.
    /// </summary>
    public async Task<ProjectionSoldeFinAnnee> PrevoirSoldeFinAnneeAsync(
        int employeurId,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var moisEcoules = today.Month;
        var moisRestants = 12 - moisEcoules;
        var solde = await SoldeCongesActuel(employeurId, today.Year, cancellationToken);
        var extra = moisEcoules == 0 || moisRestants == 0
            ? 0
            : solde.JoursPris / moisEcoules * moisRestants;
        var prevision = solde.JoursRestants - extra;
        return new ProjectionSoldeFinAnnee(
            decimal.Round(solde.JoursRestants, 2),
            decimal.Round(prevision, 2),
            moisEcoules,
            moisRestants,
            solde.JoursPris);
    }

    public async Task<IReadOnlyList<TravailTypePart>> RepartitionCongesPrisParType(
        int employeurId,
        int annee,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEmployeurExistsAsync(db, employeurId, cancellationToken);
        await EnsureCouleursAsync(db, cancellationToken);

        var rows = await db.Conges.AsNoTracking()
            .Where(c => c.EmployeurId == employeurId
                && c.Statut == StatutConge.Valide
                && c.DateDebut.Year == annee)
            .GroupBy(c => c.Type)
            .Select(g => new { Type = g.Key, Jours = g.Sum(c => c.NombreJours) })
            .ToListAsync(cancellationToken);

        var couleurs = await db.CouleursTypes.AsNoTracking()
            .ToDictionaryAsync(c => c.Type, c => c.Couleur, cancellationToken);

        return rows
            .Where(r => r.Jours > 0)
            .Select(r => new TravailTypePart(
                TravailFormat.Type(r.Type),
                r.Jours,
                couleurs.GetValueOrDefault(r.Type)))
            .OrderByDescending(r => r.Jours)
            .ToList();
    }

    public async Task<List<FichePaie>> ListerFiches12DerniersMois(
        int employeurId,
        CancellationToken cancellationToken = default)
    {
        var debut = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FichePaies.AsNoTracking()
            .Where(f => f.EmployeurId == employeurId && f.Mois >= debut)
            .OrderByDescending(f => f.Mois)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CouleurTypeConge>> ListerCouleursTypesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCouleursAsync(db, cancellationToken);
        return await db.CouleursTypes.AsNoTracking()
            .OrderBy(c => c.Type)
            .ToListAsync(cancellationToken);
    }

    public async Task EnregistrerCouleurTypeAsync(TypeConge type, string couleur, CancellationToken cancellationToken = default)
    {
        var trimmed = string.IsNullOrWhiteSpace(couleur) ? "#0d6efd" : couleur.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCouleursAsync(db, cancellationToken);
        var row = await db.CouleursTypes.FirstOrDefaultAsync(c => c.Type == type, cancellationToken)
            ?? throw new InvalidOperationException("Type de congé introuvable.");
        row.Couleur = trimmed;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static readonly (TypeConge Type, string Couleur)[] CouleursParDefaut =
    [
        (TypeConge.Paye, "#0d6efd"),
        (TypeConge.SansSolde, "#6c757d"),
        (TypeConge.Maladie, "#dc3545"),
        (TypeConge.RTT, "#20c997")
    ];

    private static async Task EnsureCouleursAsync(TravailDbContext db, CancellationToken cancellationToken)
    {
        var existants = await db.CouleursTypes.Select(c => c.Type).ToListAsync(cancellationToken);
        var added = false;
        foreach (var (type, couleur) in CouleursParDefaut)
        {
            if (existants.Contains(type))
            {
                continue;
            }

            db.CouleursTypes.Add(new CouleurTypeConge { Type = type, Couleur = couleur });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Vérifie le rôle Admin via le circuit Blazor (pas IHttpContextAccessor, souvent null en Interactive Server).</summary>
    private async Task EnsureAdminAsync()
    {
        var auth = await _authenticationStateProvider.GetAuthenticationStateAsync();
        if (auth.User.IsInRole(RoleAdmin) != true)
        {
            throw new InvalidOperationException("Seul un administrateur peut valider ou refuser un congé.");
        }
    }

    private static async Task EnsureEmployeurExistsAsync(
        TravailDbContext db,
        int employeurId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Employeurs.AnyAsync(e => e.Id == employeurId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Employeur introuvable.");
        }
    }

    private static async Task<decimal> JoursPrisValidesAsync(
        TravailDbContext db,
        int employeurId,
        int annee,
        CancellationToken cancellationToken)
    {
        return await db.Conges.AsNoTracking()
            .Where(c => c.EmployeurId == employeurId
                && c.Statut == StatutConge.Valide
                && c.DateDebut.Year == annee)
            .SumAsync(c => c.NombreJours, cancellationToken);
    }

    private static async Task EnsurePasDeChevauchementAsync(
        TravailDbContext db,
        Conge conge,
        CancellationToken cancellationToken)
    {
        var debut = conge.DateDebut.Date;
        var fin = conge.DateFin.Date;
        var chevauche = await db.Conges.AsNoTracking()
            .Where(c => c.EmployeurId == conge.EmployeurId
                && c.Statut == StatutConge.Valide
                && c.Id != conge.Id
                && c.DateDebut <= fin
                && c.DateFin >= debut)
            .OrderBy(c => c.DateDebut)
            .FirstOrDefaultAsync(cancellationToken);

        if (chevauche is not null)
        {
            throw new InvalidOperationException(
                $"Ce congé chevauche un congé déjà validé du {chevauche.DateDebut:d} au {chevauche.DateFin:d}.");
        }
    }

    private static void ValiderFichePaie(FichePaie fiche)
    {
        if (fiche.EmployeurId <= 0)
        {
            throw new InvalidOperationException("L'employeur est obligatoire.");
        }

        if (fiche.SalaireBrut < 0 || fiche.SalaireNet < 0 || fiche.TotalCotisations < 0)
        {
            throw new InvalidOperationException("Les montants ne peuvent pas être négatifs.");
        }

        if (fiche.SalaireNet > fiche.SalaireBrut)
        {
            throw new InvalidOperationException("Le salaire net ne peut pas dépasser le salaire brut.");
        }

        fiche.Mois = new DateTime(fiche.Mois.Year, fiche.Mois.Month, 1);
        fiche.SalaireBrut = decimal.Round(fiche.SalaireBrut, 2);
        fiche.SalaireNet = decimal.Round(fiche.SalaireNet, 2);
        fiche.TotalCotisations = decimal.Round(fiche.TotalCotisations, 2);
        fiche.Note = string.IsNullOrWhiteSpace(fiche.Note) ? null : fiche.Note.Trim();
        if (fiche.DateEmission == default)
        {
            fiche.DateEmission = DateTime.Now;
        }
    }

    private static void ValiderConge(Conge conge)
    {
        if (conge.EmployeurId <= 0)
        {
            throw new InvalidOperationException("L'employeur est obligatoire.");
        }

        conge.DateDebut = conge.DateDebut.Date;
        conge.DateFin = conge.DateFin.Date;

        if (conge.DateFin < conge.DateDebut)
        {
            throw new InvalidOperationException("La date de fin doit être postérieure ou égale à la date de début.");
        }

        if (conge.NombreJours <= 0)
        {
            throw new InvalidOperationException("Le nombre de jours doit être supérieur à zéro.");
        }

        conge.NombreJours = decimal.Round(conge.NombreJours, 2);
        conge.Commentaire = string.IsNullOrWhiteSpace(conge.Commentaire) ? null : conge.Commentaire.Trim();
    }
}

/// <summary>Solde d'une année : acquis persisté, pris et reste calculés.</summary>
public sealed record SoldeCongesInfo(int Annee, decimal JoursAcquis, decimal JoursPris, decimal JoursRestants);

/// <summary>Solde de l'année en cours pour un employeur encore en poste.</summary>
public sealed record TravailEmployeurSolde(int Id, string Nom, decimal JoursRestants, bool EstPrincipal);

/// <summary>Projection provisoire du solde au 31 décembre.</summary>
public sealed record ProjectionSoldeFinAnnee(
    decimal SoldeActuel,
    decimal Prevision,
    int MoisEcoules,
    int MoisRestants,
    decimal JoursPris);

/// <summary>Part du camembert (congés validés de l'année, par type).</summary>
public sealed record TravailTypePart(string Type, decimal Jours, string? Couleur);
