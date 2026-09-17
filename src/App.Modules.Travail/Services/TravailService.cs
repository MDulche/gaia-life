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

    /// <summary>
    /// Types qui consomment le solde de congés payés (<see cref="SoldeConges.JoursAcquis"/>).
    /// Seul <see cref="TypeConge.Paye"/> est déduit : Maladie et SansSolde restent dans l'historique
    /// et le camembert mais ne réduisent jamais ce solde. RTT est exclu (compteur distinct non géré ici).
    /// </summary>
    public static bool TypeConsommeSolde(TypeConge type) => type == TypeConge.Paye;

    public async Task<decimal> TotalHeuresSupMoisEnCours(int employeurId, CancellationToken cancellationToken = default)
    {
        var debut = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var fin = debut.AddMonths(1);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var total = await db.HeuresSupplementaires.AsNoTracking()
            .Where(h => h.EmployeurId == employeurId && h.Date >= debut && h.Date < fin)
            .SumAsync(h => h.DureeCalculee, cancellationToken);
        return decimal.Round(total, 2);
    }

    public async Task<List<HeureSupplementaire>> ListerHeuresSup(
        int employeurId,
        DateTime? debut = null,
        DateTime? fin = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.HeuresSupplementaires.AsNoTracking()
            .Where(h => h.EmployeurId == employeurId);

        if (debut is not null)
        {
            var d = debut.Value.Date;
            query = query.Where(h => h.Date >= d);
        }

        if (fin is not null)
        {
            var f = fin.Value.Date.AddDays(1);
            query = query.Where(h => h.Date < f);
        }

        return await query
            .OrderByDescending(h => h.Date)
            .ThenByDescending(h => h.HeureDebut)
            .ToListAsync(cancellationToken);
    }

    public async Task AjouterHeureSup(HeureSupplementaire heure, CancellationToken cancellationToken = default)
    {
        ValiderHeureSup(heure);
        heure.Date = heure.Date.Date;
        heure.DureeCalculee = decimal.Round(
            (decimal)(heure.HeureFin - heure.HeureDebut).TotalHours,
            2);
        heure.Contexte = heure.Contexte.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEmployeurExistsAsync(db, heure.EmployeurId, cancellationToken);
        db.HeuresSupplementaires.Add(heure);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierHeureSup(HeureSupplementaire heure, CancellationToken cancellationToken = default)
    {
        ValiderHeureSup(heure);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.HeuresSupplementaires.FirstOrDefaultAsync(h => h.Id == heure.Id, cancellationToken)
            ?? throw new InvalidOperationException("Heure supplémentaire introuvable.");

        if (existing.EmployeurId != heure.EmployeurId)
        {
            throw new InvalidOperationException("L'employeur ne peut pas être modifié.");
        }

        existing.Date = heure.Date.Date;
        existing.HeureDebut = heure.HeureDebut;
        existing.HeureFin = heure.HeureFin;
        existing.Contexte = heure.Contexte.Trim();
        existing.DureeCalculee = decimal.Round(
            (decimal)(heure.HeureFin - heure.HeureDebut).TotalHours,
            2);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerHeureSup(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.HeuresSupplementaires.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.HeuresSupplementaires.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Jours fériés français (métropole) pour l'année donnée.</summary>
    public IReadOnlyList<JourFerieInfo> ObtenirJoursFeries(int annee) =>
        TravailCalendrier.ObtenirJoursFeries(annee);

    /// <summary>
    /// Opportunités de pont de l'année, triées par efficacité décroissante
    /// (jours de repos / jours de congé à poser).
    /// </summary>
    public IReadOnlyList<OpportunitePont> ObtenirOpportunitesPont(int annee) =>
        TravailCalendrier.ObtenirOpportunitesPont(annee);

    /// <summary>
    /// Meilleures opportunités de pont pour l'employeur (année en cours, + suivante en fin d'année),
    /// avec indicateur <see cref="OpportunitePont.DejaPlanifie"/> si un congé couvre exactement les jours à poser.
    /// </summary>
    public async Task<IReadOnlyList<OpportunitePont>> ListerOpportunitesPontAsync(
        int employeurId,
        int max = 5,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEmployeurExistsAsync(db, employeurId, cancellationToken);

        var annee = DateTime.Today.Year;
        var opportunites = ObtenirOpportunitesPont(annee).ToList();
        if (DateTime.Today.Month >= 10)
        {
            opportunites.AddRange(ObtenirOpportunitesPont(annee + 1));
        }

        var debutFen = opportunites.SelectMany(o => o.JoursAPoser).DefaultIfEmpty(DateTime.Today).Min();
        var finFen = opportunites.SelectMany(o => o.JoursAPoser).DefaultIfEmpty(DateTime.Today).Max();

        var conges = await db.Conges.AsNoTracking()
            .Where(c => c.EmployeurId == employeurId
                && (c.Statut == StatutConge.Valide || c.Statut == StatutConge.EnAttente)
                && c.DateDebut <= finFen
                && c.DateFin >= debutFen)
            .ToListAsync(cancellationToken);

        var enrichies = opportunites
            .OrderByDescending(o => o.Efficacite)
            .ThenBy(o => o.JoursAPoser[0])
            .Select(o =>
            {
                var debut = o.JoursAPoser[0];
                var fin = o.JoursAPoser[^1];
                var deja = conges.Any(c =>
                    c.DateDebut.Date == debut.Date && c.DateFin.Date == fin.Date);
                return o with { DejaPlanifie = deja };
            })
            .Take(max)
            .ToList();

        return enrichies;
    }

    /// <summary>
    /// Planning mensuel : un jour par date du mois, avec statut prioritaire
    /// (Congé &gt; JourFerie &gt; Weekend &gt; CongeOpti &gt; Normal) et indicateur d'heures sup indépendant.
    /// </summary>
    public async Task<IReadOnlyList<JourPlanning>> ObtenirPlanning(
        int employeurId,
        int mois,
        int annee,
        CancellationToken cancellationToken = default)
    {
        if (mois is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mois), "Le mois doit être compris entre 1 et 12.");
        }

        var debutMois = new DateTime(annee, mois, 1);
        var finMois = debutMois.AddMonths(1).AddDays(-1);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureEmployeurExistsAsync(db, employeurId, cancellationToken);

        var feries = ObtenirJoursFeries(annee)
            .Where(f => f.Date.Month == mois)
            .ToDictionary(f => f.Date.Date, f => f.Libelle);

        // Ponts de l'année affichée (+ années adjacentes pour les ponts en bordure de mois).
        var joursOpti = new HashSet<DateTime>(
            ObtenirOpportunitesPont(annee)
                .SelectMany(o => o.JoursAPoser)
                .Where(d => d.Year == annee && d.Month == mois));
        if (mois == 1)
        {
            foreach (var d in ObtenirOpportunitesPont(annee - 1).SelectMany(o => o.JoursAPoser)
                         .Where(d => d.Year == annee && d.Month == mois))
            {
                joursOpti.Add(d);
            }
        }
        else if (mois == 12)
        {
            foreach (var d in ObtenirOpportunitesPont(annee + 1).SelectMany(o => o.JoursAPoser)
                         .Where(d => d.Year == annee && d.Month == mois))
            {
                joursOpti.Add(d);
            }
        }

        var conges = await db.Conges.AsNoTracking()
            .Where(c => c.EmployeurId == employeurId
                && (c.Statut == StatutConge.Valide || c.Statut == StatutConge.EnAttente)
                && c.DateDebut <= finMois
                && c.DateFin >= debutMois)
            .OrderBy(c => c.DateDebut)
            .ToListAsync(cancellationToken);

        var heuresSup = await db.HeuresSupplementaires.AsNoTracking()
            .Where(h => h.EmployeurId == employeurId
                && h.Date >= debutMois
                && h.Date < debutMois.AddMonths(1))
            .OrderBy(h => h.HeureDebut)
            .ToListAsync(cancellationToken);

        await EnsureCouleursAsync(db, cancellationToken);
        var couleurs = await db.CouleursTypes.AsNoTracking()
            .ToDictionaryAsync(c => c.Type, c => c.Couleur, cancellationToken);

        var jours = new List<JourPlanning>(finMois.Day);
        for (var jour = debutMois; jour <= finMois; jour = jour.AddDays(1))
        {
            var date = jour.Date;
            var congeDuJour = conges.FirstOrDefault(c => c.DateDebut.Date <= date && c.DateFin.Date >= date);
            var hsDuJour = heuresSup
                .Where(h => h.Date.Date == date)
                .Select(h => new HeureSupPlanningInfo(h.HeureDebut, h.HeureFin, h.DureeCalculee, h.Contexte))
                .ToList();

            StatutJourPlanning statut;
            TypeConge? typeConge = null;
            string? libelleFerie = null;
            string? couleur = null;

            if (congeDuJour is not null)
            {
                statut = StatutJourPlanning.Conge;
                typeConge = congeDuJour.Type;
                couleurs.TryGetValue(congeDuJour.Type, out couleur);
                feries.TryGetValue(date, out libelleFerie);
            }
            else if (feries.TryGetValue(date, out libelleFerie))
            {
                statut = StatutJourPlanning.JourFerie;
                couleur = TravailCalendrier.CouleurJourFerie;
            }
            else if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                statut = StatutJourPlanning.Weekend;
            }
            else if (joursOpti.Contains(date))
            {
                statut = StatutJourPlanning.CongeOpti;
                couleur = TravailCalendrier.CouleurCongeOpti;
            }
            else
            {
                statut = StatutJourPlanning.Normal;
            }

            jours.Add(new JourPlanning(
                date,
                statut,
                typeConge,
                libelleFerie,
                hsDuJour.Count > 0,
                couleur,
                hsDuJour));
        }

        return jours;
    }

    public async Task ModifierFichePaie(FichePaie fiche, CancellationToken cancellationToken = default)
    {
        ValiderFichePaie(fiche);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.FichePaies.FirstOrDefaultAsync(f => f.Id == fiche.Id, cancellationToken)
            ?? throw new InvalidOperationException("Fiche de paie introuvable.");

        if (existing.EmployeurId != fiche.EmployeurId)
        {
            throw new InvalidOperationException("L'employeur ne peut pas être modifié.");
        }

        var conflit = await db.FichePaies.AnyAsync(
            f => f.EmployeurId == fiche.EmployeurId && f.Mois == fiche.Mois && f.Id != fiche.Id,
            cancellationToken);
        if (conflit)
        {
            throw new InvalidOperationException("Une fiche de paie existe déjà pour ce mois.");
        }

        existing.Mois = fiche.Mois;
        existing.SalaireBrut = fiche.SalaireBrut;
        existing.SalaireNet = fiche.SalaireNet;
        existing.TotalCotisations = fiche.TotalCotisations;
        existing.Note = fiche.Note;
        existing.DateEmission = fiche.DateEmission == default ? existing.DateEmission : fiche.DateEmission;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerFichePaie(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.FichePaies.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.FichePaies.Remove(existing);
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
            await EnsureSoldeSuffisantPourValidationAsync(db, conge, cancellationToken);
        }

        conge.Statut = nouveauStatut;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reste = jours acquis − portion des congés <see cref="TypeConge.Paye"/> validés
    /// qui tombe réellement dans l'année (répartition si le congé chevauche deux années civiles).
    /// Maladie / SansSolde / RTT : jamais déduits (voir <see cref="TypeConsommeSolde"/>).
    /// </summary>
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

        if (actifs.Count == 0)
        {
            return [];
        }

        var ids = actifs.Select(e => e.Id).ToList();
        var soldes = await db.SoldeConges.AsNoTracking()
            .Where(s => ids.Contains(s.EmployeurId) && s.Annee == annee)
            .ToDictionaryAsync(s => s.EmployeurId, s => s.JoursAcquis, cancellationToken);

        var yearStart = new DateTime(annee, 1, 1);
        var yearEnd = new DateTime(annee, 12, 31);
        var conges = await db.Conges.AsNoTracking()
            .Where(c => ids.Contains(c.EmployeurId)
                && c.Statut == StatutConge.Valide
                && c.Type == TypeConge.Paye
                && c.DateDebut <= yearEnd
                && c.DateFin >= yearStart)
            .ToListAsync(cancellationToken);

        var joursPrisParEmployeur = conges
            .GroupBy(c => c.EmployeurId)
            .ToDictionary(
                g => g.Key,
                g => decimal.Round(g.Sum(c => JoursConsommesDansAnnee(c, annee)), 2));

        var rows = new List<TravailEmployeurSolde>(actifs.Count);
        var premier = true;
        foreach (var employeur in actifs)
        {
            var joursAcquis = soldes.GetValueOrDefault(employeur.Id);
            var joursPris = joursPrisParEmployeur.GetValueOrDefault(employeur.Id);
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
        var yearStart = new DateTime(annee, 1, 1);
        var yearEnd = new DateTime(annee, 12, 31);
        var conges = await db.Conges.AsNoTracking()
            .Where(c => c.EmployeurId == employeurId
                && c.Statut == StatutConge.Valide
                && c.Type == TypeConge.Paye
                && c.DateDebut <= yearEnd
                && c.DateFin >= yearStart)
            .ToListAsync(cancellationToken);

        return decimal.Round(conges.Sum(c => JoursConsommesDansAnnee(c, annee)), 2);
    }

    /// <summary>
    /// Portion de <see cref="Conge.NombreJours"/> attribuée à une année civile, au prorata des
    /// jours ouvrés (lun–ven) de l'intersection [DateDebut, DateFin] ∩ année.
    /// </summary>
    internal static decimal JoursConsommesDansAnnee(Conge conge, int annee)
    {
        if (!TypeConsommeSolde(conge.Type))
        {
            return 0;
        }

        var yearStart = new DateTime(annee, 1, 1);
        var yearEnd = new DateTime(annee, 12, 31);
        var debut = conge.DateDebut.Date;
        var fin = conge.DateFin.Date;
        if (fin < yearStart || debut > yearEnd)
        {
            return 0;
        }

        var totalOuvres = TravailCalendrier.CompterJoursOuvres(debut, fin);
        var portionDebut = debut > yearStart ? debut : yearStart;
        var portionFin = fin < yearEnd ? fin : yearEnd;
        var portionOuvres = TravailCalendrier.CompterJoursOuvres(portionDebut, portionFin);

        if (totalOuvres <= 0)
        {
            return debut.Year == annee ? conge.NombreJours : 0;
        }

        return decimal.Round(conge.NombreJours * (portionOuvres / totalOuvres), 2);
    }

    private static async Task EnsureSoldeSuffisantPourValidationAsync(
        TravailDbContext db,
        Conge conge,
        CancellationToken cancellationToken)
    {
        if (!TypeConsommeSolde(conge.Type))
        {
            return;
        }

        var fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
        for (var annee = conge.DateDebut.Year; annee <= conge.DateFin.Year; annee++)
        {
            var demande = JoursConsommesDansAnnee(conge, annee);
            if (demande <= 0)
            {
                continue;
            }

            var soldeRow = await db.SoldeConges.AsNoTracking()
                .FirstOrDefaultAsync(s => s.EmployeurId == conge.EmployeurId && s.Annee == annee, cancellationToken);
            var acquis = soldeRow?.JoursAcquis ?? 0;
            var dejaPris = await JoursPrisValidesAsync(db, conge.EmployeurId, annee, cancellationToken);
            var disponible = acquis - dejaPris;
            if (demande > disponible)
            {
                throw new InvalidOperationException(
                    $"Solde insuffisant pour {annee} : {disponible.ToString("0.##", fr)} j disponibles, " +
                    $"{demande.ToString("0.##", fr)} j demandés (congés payés uniquement).");
            }
        }
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
                && (c.Statut == StatutConge.Valide || c.Statut == StatutConge.EnAttente)
                && c.Id != conge.Id
                && c.DateDebut <= fin
                && c.DateFin >= debut)
            .OrderBy(c => c.DateDebut)
            .FirstOrDefaultAsync(cancellationToken);

        if (chevauche is not null)
        {
            var libelle = chevauche.Statut == StatutConge.EnAttente
                ? "une demande déjà en attente"
                : "un congé déjà validé";
            throw new InvalidOperationException(
                $"Cette période chevauche {libelle} du {chevauche.DateDebut:d} au {chevauche.DateFin:d}.");
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

    private static void ValiderHeureSup(HeureSupplementaire heure)
    {
        if (heure.EmployeurId <= 0)
        {
            throw new InvalidOperationException("L'employeur est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(heure.Contexte))
        {
            throw new InvalidOperationException("Le contexte est obligatoire.");
        }

        if (heure.HeureFin <= heure.HeureDebut)
        {
            throw new InvalidOperationException("L'heure de fin doit être postérieure à l'heure de début (même journée).");
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

/// <summary>Statut principal d'un jour sur le planning mensuel.</summary>
public enum StatutJourPlanning
{
    Normal = 0,
    Weekend = 1,
    JourFerie = 2,
    Conge = 3,
    CongeOpti = 4
}

/// <summary>Heure supplémentaire affichée dans le détail d'un jour du planning.</summary>
public sealed record HeureSupPlanningInfo(
    TimeSpan HeureDebut,
    TimeSpan HeureFin,
    decimal Duree,
    string Contexte);

/// <summary>
/// Jour du planning mensuel. <see cref="HeureSupPresente"/> est indépendant du
/// <see cref="Statut"/> (priorité Congé &gt; JourFerie &gt; Weekend &gt; CongeOpti &gt; Normal).
/// </summary>
public sealed record JourPlanning(
    DateTime Date,
    StatutJourPlanning Statut,
    TypeConge? TypeConge,
    string? LibelleJourFerie,
    bool HeureSupPresente,
    string? Couleur,
    IReadOnlyList<HeureSupPlanningInfo> HeuresSup);
