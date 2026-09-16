using App.Modules.Finance.Data;
using App.Modules.Finance.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Finance.Services;

/// <summary>
/// Accès métier Finances. Chaque méthode ouvre son propre contexte via factory
/// (évite les accès concurrents Blazor menu + page).
/// </summary>
public sealed class FinanceService
{
    private readonly IDbContextFactory<FinanceDbContext> _dbFactory;

    public FinanceService(IDbContextFactory<FinanceDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<Compte>> ListerComptesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Comptes.AsNoTracking()
            .OrderByDescending(c => c.EstPrincipal)
            .ThenBy(c => c.Nom)
            .ToListAsync(cancellationToken);
    }

    public async Task<Compte?> GetCompteAsync(int compteId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Comptes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == compteId, cancellationToken);
    }

    public async Task<Compte> AjouterCompteAsync(string nom, decimal soldeInitial, CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom du compte est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var estPrincipal = !await db.Comptes.AnyAsync(cancellationToken);
        var compte = new Compte
        {
            Nom = trimmed,
            SoldeInitial = soldeInitial,
            EstPrincipal = estPrincipal,
            DateCreation = DateTime.Now
        };

        db.Comptes.Add(compte);
        await db.SaveChangesAsync(cancellationToken);
        return compte;
    }

    /// <summary>Solde courant = solde initial + entrées − sorties.</summary>
    public async Task<decimal> SoldeActuel(int compteId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await SoldeActuelAsync(db, compteId, cancellationToken);
    }

    public async Task<decimal> SoldeTotalAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var comptes = await db.Comptes.AsNoTracking().Select(c => c.Id).ToListAsync(cancellationToken);
        decimal total = 0;
        foreach (var compteId in comptes)
        {
            total += await SoldeActuelAsync(db, compteId, cancellationToken);
        }

        return total;
    }

    public async Task<IReadOnlyList<Transaction>> ListerTransactions(
        int compteId,
        DateTime? dateDebut = null,
        DateTime? dateFin = null,
        string? categorie = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = FiltrerTransactions(db.Transactions.AsNoTracking().Where(t => t.CompteId == compteId), dateDebut, dateFin, categorie);
        return await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Transaction> Items, int Total)> ListerTransactionsPaged(
        int compteId,
        DateTime? dateDebut,
        DateTime? dateFin,
        string? categorie,
        bool dateDesc,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = FiltrerTransactions(db.Transactions.AsNoTracking().Where(t => t.CompteId == compteId), dateDebut, dateFin, categorie);
        var total = await query.CountAsync(cancellationToken);
        query = dateDesc
            ? query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            : query.OrderBy(t => t.Date).ThenBy(t => t.Id);
        var items = await query.Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<Transaction?> GetTransactionAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    /// <summary>Crée la catégorie à la volée si le nom n'existe pas encore.</summary>
    public async Task AjouterTransaction(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ValiderTransaction(transaction);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCategorieCoreAsync(db, transaction.Categorie, cancellationToken: cancellationToken);
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ValiderTransaction(transaction);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Transactions.FirstOrDefaultAsync(t => t.Id == transaction.Id, cancellationToken)
            ?? throw new InvalidOperationException("Transaction introuvable.");

        await EnsureCategorieCoreAsync(db, transaction.Categorie, cancellationToken: cancellationToken);
        existing.Date = transaction.Date.Date;
        existing.Montant = decimal.Round(transaction.Montant, 2);
        existing.Type = transaction.Type;
        existing.Categorie = transaction.Categorie.Trim();
        existing.Note = string.IsNullOrWhiteSpace(transaction.Note) ? null : transaction.Note.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerTransaction(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.Transactions.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Categorie>> ListerCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking().OrderBy(c => c.Nom).ToListAsync(cancellationToken);
    }

    public async Task<Categorie> EnsureCategorieAsync(string nom, string? couleur = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await EnsureCategorieCoreAsync(db, nom, couleur, cancellationToken);
    }

    public async Task AjouterCategorieAsync(string nom, string? couleur, CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom de la catégorie est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Categories.AnyAsync(c => c.Nom == trimmed, cancellationToken))
        {
            throw new InvalidOperationException("Une catégorie porte déjà ce nom.");
        }

        db.Categories.Add(new Categorie
        {
            Nom = trimmed,
            Couleur = string.IsNullOrWhiteSpace(couleur) ? null : couleur.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Transaction>> DernieresTransactionsAsync(int nombre, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Transactions.AsNoTracking()
            .Include(t => t.Compte)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .Take(nombre)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Totaux par catégorie de sorties sur une période (graphique camembert).</summary>
    public async Task<IReadOnlyList<CategorieMontant>> RepartitionParCategorie(DateTime debut, DateTime fin, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var finExclusive = fin.Date.AddDays(1);
        var rows = await db.Transactions.AsNoTracking()
            .Where(t => t.Type == TypeTransaction.Sortie && t.Date >= debut.Date && t.Date < finExclusive)
            .GroupBy(t => t.Categorie)
            .Select(g => new { Categorie = g.Key, Total = g.Sum(t => t.Montant) })
            .ToListAsync(cancellationToken);

        var couleurs = await db.Categories.AsNoTracking()
            .ToDictionaryAsync(c => c.Nom, c => c.Couleur, cancellationToken);

        return rows
            .OrderByDescending(r => r.Total)
            .Select(r => new CategorieMontant(r.Categorie, r.Total, couleurs.GetValueOrDefault(r.Categorie)))
            .ToList();
    }

    public async Task<IReadOnlyList<MoisTotaux>> TotauxParMois(int nombreDeMois, CancellationToken cancellationToken = default)
    {
        if (nombreDeMois < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nombreDeMois));
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var today = DateTime.Today;
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(1 - nombreDeMois);
        var transactions = await db.Transactions.AsNoTracking()
            .Where(t => t.Date >= start)
            .ToListAsync(cancellationToken);

        var result = new List<MoisTotaux>(nombreDeMois);
        for (var i = 0; i < nombreDeMois; i++)
        {
            var mois = start.AddMonths(i);
            var next = mois.AddMonths(1);
            var duMois = transactions.Where(t => t.Date >= mois && t.Date < next);
            result.Add(new MoisTotaux(
                mois,
                duMois.Where(t => t.Type == TypeTransaction.Entree).Sum(t => t.Montant),
                duMois.Where(t => t.Type == TypeTransaction.Sortie).Sum(t => t.Montant)));
        }

        return result;
    }

    private static async Task<decimal> SoldeActuelAsync(FinanceDbContext db, int compteId, CancellationToken cancellationToken)
    {
        var compte = await db.Comptes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == compteId, cancellationToken)
            ?? throw new InvalidOperationException("Compte introuvable.");

        var mouvements = await db.Transactions.AsNoTracking()
            .Where(t => t.CompteId == compteId)
            .Select(t => t.Type == TypeTransaction.Entree ? t.Montant : -t.Montant)
            .ToListAsync(cancellationToken);

        return compte.SoldeInitial + mouvements.Sum();
    }

    private static async Task<Categorie> EnsureCategorieCoreAsync(
        FinanceDbContext db,
        string nom,
        string? couleur = null,
        CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom de la catégorie est obligatoire.");
        }

        var existing = await db.Categories.FirstOrDefaultAsync(c => c.Nom == trimmed, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new Categorie { Nom = trimmed, Couleur = couleur };
        db.Categories.Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task ModifierCompteAsync(int id, string nom, decimal soldeInitial, CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom du compte est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var compte = await db.Comptes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Compte introuvable.");
        compte.Nom = trimmed;
        compte.SoldeInitial = decimal.Round(soldeInitial, 2);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DefinirComptePrincipalAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var compte = await db.Comptes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Compte introuvable.");

        foreach (var autre in await db.Comptes.Where(c => c.EstPrincipal).ToListAsync(cancellationToken))
        {
            autre.EstPrincipal = false;
        }

        compte.EstPrincipal = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerCompteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.ChargesMensuelles.AnyAsync(c => c.CompteId == id, cancellationToken))
        {
            throw new InvalidOperationException("Ce compte a des charges mensuelles associées. Supprimez-les d'abord.");
        }

        var compte = await db.Comptes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (compte is null)
        {
            return;
        }

        var etaitPrincipal = compte.EstPrincipal;
        db.Comptes.Remove(compte);
        await db.SaveChangesAsync(cancellationToken);

        if (etaitPrincipal)
        {
            var suivant = await db.Comptes.OrderBy(c => c.Nom).FirstOrDefaultAsync(cancellationToken);
            if (suivant is not null)
            {
                suivant.EstPrincipal = true;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public async Task<List<ChargeMensuelle>> ListerChargesMensuellesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ChargesMensuelles.AsNoTracking()
            .Include(c => c.Compte)
            .OrderBy(c => c.Nom)
            .ToListAsync(cancellationToken);
    }

    public async Task AjouterChargeMensuelleAsync(string nom, decimal montant, int compteId, CancellationToken cancellationToken = default)
    {
        ValiderCharge(nom, montant);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Comptes.AnyAsync(c => c.Id == compteId, cancellationToken))
        {
            throw new InvalidOperationException("Compte associé introuvable.");
        }

        db.ChargesMensuelles.Add(new ChargeMensuelle
        {
            Nom = nom.Trim(),
            Montant = decimal.Round(montant, 2),
            CompteId = compteId
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierChargeMensuelleAsync(int id, string nom, decimal montant, int compteId, CancellationToken cancellationToken = default)
    {
        ValiderCharge(nom, montant);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var charge = await db.ChargesMensuelles.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Charge mensuelle introuvable.");
        if (!await db.Comptes.AnyAsync(c => c.Id == compteId, cancellationToken))
        {
            throw new InvalidOperationException("Compte associé introuvable.");
        }

        charge.Nom = nom.Trim();
        charge.Montant = decimal.Round(montant, 2);
        charge.CompteId = compteId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerChargeMensuelleAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var charge = await db.ChargesMensuelles.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (charge is null)
        {
            return;
        }

        db.ChargesMensuelles.Remove(charge);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ChargeAnnuelle>> ListerChargesAnnuellesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ChargesAnnuelles.AsNoTracking()
            .OrderBy(c => c.MoisEcheance)
            .ThenBy(c => c.Nom)
            .ToListAsync(cancellationToken);
    }

    public async Task AjouterChargeAnnuelleAsync(string nom, decimal montant, int moisEcheance, CancellationToken cancellationToken = default)
    {
        ValiderCharge(nom, montant);
        ValiderMois(moisEcheance);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.ChargesAnnuelles.Add(new ChargeAnnuelle
        {
            Nom = nom.Trim(),
            Montant = decimal.Round(montant, 2),
            MoisEcheance = moisEcheance
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierChargeAnnuelleAsync(int id, string nom, decimal montant, int moisEcheance, CancellationToken cancellationToken = default)
    {
        ValiderCharge(nom, montant);
        ValiderMois(moisEcheance);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var charge = await db.ChargesAnnuelles.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Charge annuelle introuvable.");
        charge.Nom = nom.Trim();
        charge.Montant = decimal.Round(montant, 2);
        charge.MoisEcheance = moisEcheance;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerChargeAnnuelleAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var charge = await db.ChargesAnnuelles.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (charge is null)
        {
            return;
        }

        db.ChargesAnnuelles.Remove(charge);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierCategorieAsync(int id, string nom, string? couleur, CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom de la catégorie est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var categorie = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Catégorie introuvable.");

        var ancienNom = categorie.Nom;
        var collision = await db.Categories.AnyAsync(c => c.Id != id && c.Nom == trimmed, cancellationToken);
        if (collision)
        {
            throw new InvalidOperationException("Une catégorie porte déjà ce nom.");
        }

        categorie.Nom = trimmed;
        categorie.Couleur = string.IsNullOrWhiteSpace(couleur) ? null : couleur.Trim();
        if (!string.Equals(ancienNom, trimmed, StringComparison.Ordinal))
        {
            await db.Transactions
                .Where(t => t.Categorie == ancienNom)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Categorie, trimmed), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerCategorieAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var categorie = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categorie is null)
        {
            return;
        }

        db.Categories.Remove(categorie);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetJoursMoyennePrevisionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var parametre = await ObtenirOuCreerParametreAsync(db, cancellationToken);
        return parametre.JoursMoyennePrevision;
    }

    public async Task SetJoursMoyennePrevisionAsync(int jours, CancellationToken cancellationToken = default)
    {
        if (jours is < 1 or > 366)
        {
            throw new InvalidOperationException("La période de moyenne doit être comprise entre 1 et 366 jours.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var parametre = await ObtenirOuCreerParametreAsync(db, cancellationToken);
        parametre.JoursMoyennePrevision = jours;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Prévision provisoire du solde de fin de mois :
    /// solde actuel total + (moyenne quotidienne des entrées − moyenne quotidienne des sorties)
    /// sur les N derniers jours, multipliée par les jours restants du mois en cours.
    /// Cette méthode de calcul est un placeholder de maquette et pourra être affinée plus tard
    /// (saisonnalité, charges connues, exclusion des exceptions, etc.).
    /// </summary>
    public async Task<PrevisionSoldeFinDeMois> PrevoirSoldeFinDeMoisAsync(CancellationToken cancellationToken = default)
    {
        var joursMoyenne = await GetJoursMoyennePrevisionAsync(cancellationToken);
        var today = DateTime.Today;
        var debutFenetre = today.AddDays(1 - joursMoyenne);
        var finExclusive = today.AddDays(1);
        var joursRestants = DateTime.DaysInMonth(today.Year, today.Month) - today.Day;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var mouvements = await db.Transactions.AsNoTracking()
            .Where(t => t.Date >= debutFenetre && t.Date < finExclusive)
            .Select(t => new { t.Type, t.Montant })
            .ToListAsync(cancellationToken);

        var entrees = mouvements.Where(t => t.Type == TypeTransaction.Entree).Sum(t => t.Montant);
        var sorties = mouvements.Where(t => t.Type == TypeTransaction.Sortie).Sum(t => t.Montant);
        var moyenneEntrees = joursMoyenne == 0 ? 0 : entrees / joursMoyenne;
        var moyenneSorties = joursMoyenne == 0 ? 0 : sorties / joursMoyenne;
        var soldeActuel = await SoldeTotalAsync(cancellationToken);
        var prevision = soldeActuel + (moyenneEntrees - moyenneSorties) * joursRestants;

        return new PrevisionSoldeFinDeMois(
            decimal.Round(soldeActuel, 2),
            decimal.Round(prevision, 2),
            joursMoyenne,
            joursRestants,
            decimal.Round(moyenneEntrees, 2),
            decimal.Round(moyenneSorties, 2));
    }

    private static async Task<FinanceParametre> ObtenirOuCreerParametreAsync(
        FinanceDbContext db,
        CancellationToken cancellationToken)
    {
        var parametre = await db.Parametres.FirstOrDefaultAsync(cancellationToken);
        if (parametre is not null)
        {
            return parametre;
        }

        parametre = new FinanceParametre { JoursMoyennePrevision = 30 };
        db.Parametres.Add(parametre);
        await db.SaveChangesAsync(cancellationToken);
        return parametre;
    }

    private static void ValiderCharge(string nom, decimal montant)
    {
        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new InvalidOperationException("Le nom de la charge est obligatoire.");
        }

        if (montant <= 0)
        {
            throw new InvalidOperationException("Le montant doit être strictement positif.");
        }
    }

    private static void ValiderMois(int mois)
    {
        if (mois is < 1 or > 12)
        {
            throw new InvalidOperationException("Le mois d'échéance doit être compris entre 1 et 12.");
        }
    }

    private static IQueryable<Transaction> FiltrerTransactions(
        IQueryable<Transaction> query,
        DateTime? dateDebut,
        DateTime? dateFin,
        string? categorie)
    {
        if (dateDebut is not null)
        {
            query = query.Where(t => t.Date >= dateDebut.Value.Date);
        }

        if (dateFin is not null)
        {
            var finExclusive = dateFin.Value.Date.AddDays(1);
            query = query.Where(t => t.Date < finExclusive);
        }

        if (!string.IsNullOrWhiteSpace(categorie))
        {
            query = query.Where(t => t.Categorie == categorie);
        }

        return query;
    }

    private static void ValiderTransaction(Transaction transaction)
    {
        if (transaction.CompteId <= 0)
        {
            throw new InvalidOperationException("Le compte est obligatoire.");
        }

        if (transaction.Montant <= 0)
        {
            throw new InvalidOperationException("Le montant doit être strictement positif.");
        }

        if (transaction.Date.Date > DateTime.Today.AddDays(1))
        {
            throw new InvalidOperationException("La date ne peut pas être plus d'un jour dans le futur.");
        }

        if (string.IsNullOrWhiteSpace(transaction.Categorie))
        {
            throw new InvalidOperationException("La catégorie est obligatoire.");
        }

        transaction.Date = transaction.Date.Date;
        transaction.Montant = decimal.Round(transaction.Montant, 2);
        transaction.Categorie = transaction.Categorie.Trim();
        transaction.Note = string.IsNullOrWhiteSpace(transaction.Note) ? null : transaction.Note.Trim();
    }
}

/// <summary>Total des sorties d'une catégorie (synthèse).</summary>
public sealed record CategorieMontant(string Categorie, decimal Total, string? Couleur);

/// <summary>Entrées / sorties d'un mois calendaire.</summary>
public sealed record MoisTotaux(DateTime Mois, decimal Entrees, decimal Sorties);

/// <summary>Résultat de la prévision provisoire de fin de mois.</summary>
public sealed record PrevisionSoldeFinDeMois(
    decimal SoldeActuel,
    decimal Prevision,
    int JoursMoyenne,
    int JoursRestants,
    decimal MoyenneEntreesParJour,
    decimal MoyenneSortiesParJour);
