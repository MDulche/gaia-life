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
    /// <summary>Libellé affiché sur les deux jambes d'un virement interne (indicateur = <see cref="Transaction.EstVirementInterne"/>).</summary>
    public const string CategorieVirementInterne = "Virement interne";

    /// <summary>Catégorie de repli lors de la suppression d'une catégorie encore référencée.</summary>
    public const string CategorieNonCategorise = "Non catégorisé";

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

    public async Task<Compte> AjouterCompteAsync(
        string nom,
        decimal soldeInitial,
        TypeCompte type = TypeCompte.Courant,
        CancellationToken cancellationToken = default)
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
            Type = type,
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

    /// <summary>Somme des soldes des comptes de type Épargne.</summary>
    public async Task<decimal> TotalEpargne(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.Comptes.AsNoTracking()
            .Where(c => c.Type == TypeCompte.Epargne)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        decimal total = 0;
        foreach (var compteId in ids)
        {
            total += await SoldeActuelAsync(db, compteId, cancellationToken);
        }

        return total;
    }

    public async Task<List<Compte>> ListerComptesEpargneAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Comptes.AsNoTracking()
            .Where(c => c.Type == TypeCompte.Epargne)
            .OrderBy(c => c.Nom)
            .ToListAsync(cancellationToken);
    }

    public async Task<ObjectifEpargne?> GetObjectifEpargneAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.ObjectifsEpargne.AsNoTracking()
            .Include(o => o.Compte)
            .OrderBy(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task EnregistrerObjectifEpargneAsync(
        string nom,
        decimal montantCible,
        DateTime? dateCibleOptionnelle,
        int compteId,
        CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom de l'objectif est obligatoire.");
        }

        if (montantCible <= 0)
        {
            throw new InvalidOperationException("Le montant cible doit être strictement positif.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var compte = await db.Comptes.FirstOrDefaultAsync(c => c.Id == compteId, cancellationToken)
            ?? throw new InvalidOperationException("Compte introuvable.");
        if (compte.Type != TypeCompte.Epargne)
        {
            throw new InvalidOperationException("L'objectif doit être lié à un compte d'épargne.");
        }

        var existant = await db.ObjectifsEpargne.OrderBy(o => o.Id).FirstOrDefaultAsync(cancellationToken);
        if (existant is null)
        {
            db.ObjectifsEpargne.Add(new ObjectifEpargne
            {
                Nom = trimmed,
                MontantCible = decimal.Round(montantCible, 2),
                DateCibleOptionnelle = dateCibleOptionnelle?.Date,
                CompteId = compteId
            });
        }
        else
        {
            existant.Nom = trimmed;
            existant.MontantCible = decimal.Round(montantCible, 2);
            existant.DateCibleOptionnelle = dateCibleOptionnelle?.Date;
            existant.CompteId = compteId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerObjectifEpargneAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existant = await db.ObjectifsEpargne.OrderBy(o => o.Id).FirstOrDefaultAsync(cancellationToken);
        if (existant is null)
        {
            return;
        }

        db.ObjectifsEpargne.Remove(existant);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Progression vers l'objectif actif. L'estimation de date utilise la moyenne des versements
    /// (entrées) des 3 derniers mois calendaires sur le compte lié ; indisponible si cette moyenne
    /// est nulle ou négative.
    /// </summary>
    public async Task<ProgressionObjectifEpargne> ProgressionObjectif(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var objectif = await db.ObjectifsEpargne.AsNoTracking()
            .OrderBy(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (objectif is null)
        {
            return ProgressionObjectifEpargne.Aucun();
        }

        var actuel = await SoldeActuelAsync(db, objectif.CompteId, cancellationToken);
        var pourcentage = objectif.MontantCible <= 0
            ? 0
            : decimal.Round(actuel / objectif.MontantCible * 100, 1);

        var today = DateTime.Today;
        var debut = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
        var versements = await db.Transactions.AsNoTracking()
            .Where(t => t.CompteId == objectif.CompteId
                && t.Type == TypeTransaction.Entree
                && t.Date >= debut)
            .SumAsync(t => (decimal?)t.Montant, cancellationToken) ?? 0;
        var moyenne = versements / 3;

        DateTime? dateEstimee = null;
        var estimationDisponible = false;
        if (actuel >= objectif.MontantCible)
        {
            dateEstimee = today;
            estimationDisponible = true;
        }
        else if (moyenne > 0)
        {
            var restant = objectif.MontantCible - actuel;
            var mois = (int)Math.Ceiling(restant / moyenne);
            dateEstimee = new DateTime(today.Year, today.Month, 1).AddMonths(mois);
            estimationDisponible = true;
        }

        return new ProgressionObjectifEpargne(
            true,
            objectif.Nom,
            objectif.MontantCible,
            actuel,
            pourcentage,
            dateEstimee,
            estimationDisponible,
            actuel >= objectif.MontantCible);
    }

    /// <summary>
    /// Comparaison des versements (entrées) sur comptes épargne, mois vs mois précédent.
    /// Inclut volontairement les entrées <see cref="Transaction.EstVirementInterne"/> : ce sont
    /// précisément les versements mesurés. Les totaux « tendances » externes passent par
    /// <see cref="TotauxMensuels"/>, qui exclut les virements internes.
    /// </summary>
    public async Task<TendanceVersementsEpargne> TendanceVersementsEpargne(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.Comptes.AsNoTracking()
            .Where(c => c.Type == TypeCompte.Epargne)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var ceMoisDebut = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var precedentDebut = ceMoisDebut.AddMonths(-1);
        var ceMois = await VersementsPeriodeAsync(db, ids, ceMoisDebut, ceMoisDebut.AddMonths(1), cancellationToken);
        var precedent = await VersementsPeriodeAsync(db, ids, precedentDebut, ceMoisDebut, cancellationToken);
        decimal? variation = precedent == 0
            ? null
            : decimal.Round((ceMois - precedent) / precedent * 100, 1);

        return new TendanceVersementsEpargne(ceMois, precedent, variation);
    }

    private static async Task<decimal> VersementsPeriodeAsync(
        FinanceDbContext db,
        List<int> compteIds,
        DateTime debut,
        DateTime finExclusive,
        CancellationToken cancellationToken)
    {
        if (compteIds.Count == 0)
        {
            return 0;
        }

        return await db.Transactions.AsNoTracking()
            .Where(t => compteIds.Contains(t.CompteId)
                && t.Type == TypeTransaction.Entree
                && t.Date >= debut
                && t.Date < finExclusive)
            .SumAsync(t => (decimal?)t.Montant, cancellationToken) ?? 0;
    }

    /// <summary>
    /// Crée atomiquement une sortie sur <paramref name="compteSourceId"/> et une entrée sur
    /// <paramref name="compteDestId"/>, liées par le même <see cref="Transaction.TransfertId"/>
    /// et marquées <see cref="Transaction.EstVirementInterne"/>.
    /// </summary>
    public async Task<(Transaction Sortie, Transaction Entree)> EffectuerVirement(
        int compteSourceId,
        int compteDestId,
        decimal montant,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        if (compteSourceId <= 0 || compteDestId <= 0)
        {
            throw new InvalidOperationException("Les comptes source et destination sont obligatoires.");
        }

        if (compteSourceId == compteDestId)
        {
            throw new InvalidOperationException("Le compte source et le compte destination doivent être distincts.");
        }

        if (montant <= 0)
        {
            throw new InvalidOperationException("Le montant doit être strictement positif.");
        }

        var dateNorm = date.Date;
        if (dateNorm > DateTime.Today.AddDays(1))
        {
            throw new InvalidOperationException("La date ne peut pas être plus d'un jour dans le futur.");
        }

        var montantNorm = decimal.Round(montant, 2);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var source = await db.Comptes.FirstOrDefaultAsync(c => c.Id == compteSourceId, cancellationToken)
            ?? throw new InvalidOperationException("Compte source introuvable.");
        var dest = await db.Comptes.FirstOrDefaultAsync(c => c.Id == compteDestId, cancellationToken)
            ?? throw new InvalidOperationException("Compte destination introuvable.");

        await EnsureCategorieCoreAsync(db, CategorieVirementInterne, "#20c997", cancellationToken);

        var transfertId = Guid.NewGuid();
        var sortie = new Transaction
        {
            CompteId = compteSourceId,
            Date = dateNorm,
            Montant = montantNorm,
            Type = TypeTransaction.Sortie,
            Categorie = CategorieVirementInterne,
            Note = $"Versement vers {dest.Nom}",
            EstVirementInterne = true,
            TransfertId = transfertId
        };
        var entree = new Transaction
        {
            CompteId = compteDestId,
            Date = dateNorm,
            Montant = montantNorm,
            Type = TypeTransaction.Entree,
            Categorie = CategorieVirementInterne,
            Note = $"Versement depuis {source.Nom}",
            EstVirementInterne = true,
            TransfertId = transfertId
        };

        // Un seul SaveChanges = une transaction DB (compatible EnableRetryOnFailure).
        db.Transactions.Add(sortie);
        db.Transactions.Add(entree);
        await db.SaveChangesAsync(cancellationToken);
        return (sortie, entree);
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

    /// <summary>Crée la catégorie à la volée si le nom n'existe pas encore. Les virements passent par <see cref="EffectuerVirement"/>.</summary>
    public async Task AjouterTransaction(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ValiderTransaction(transaction);
        transaction.EstVirementInterne = false;
        transaction.TransfertId = null;
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

        if (existing.EstVirementInterne || existing.TransfertId is not null)
        {
            throw new InvalidOperationException(
                "Cette transaction fait partie d'un virement interne. Supprimez le virement puis recréez-en un nouveau.");
        }

        await EnsureCategorieCoreAsync(db, transaction.Categorie, cancellationToken: cancellationToken);
        existing.Date = transaction.Date.Date;
        existing.Montant = decimal.Round(transaction.Montant, 2);
        existing.Type = transaction.Type;
        existing.Categorie = transaction.Categorie.Trim();
        existing.Note = string.IsNullOrWhiteSpace(transaction.Note) ? null : transaction.Note.Trim();
        existing.EstVirementInterne = false;
        existing.TransfertId = null;
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

        if (existing.TransfertId is Guid transfertId)
        {
            var jumelles = await db.Transactions
                .Where(t => t.TransfertId == transfertId)
                .ToListAsync(cancellationToken);
            db.Transactions.RemoveRange(jumelles);
        }
        else
        {
            db.Transactions.Remove(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Categorie>> ListerCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCategorieCoreAsync(db, CategorieVirementInterne, "#20c997", cancellationToken);
        await EnsureCategorieCoreAsync(db, CategorieNonCategorise, "#6c757d", cancellationToken);
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

        if (EstCategorieSysteme(trimmed))
        {
            throw new InvalidOperationException("Ce nom est réservé à une catégorie système.");
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

    /// <summary>
    /// Totaux par catégorie de sorties sur une période (graphique camembert).
    /// Les virements internes (<see cref="Transaction.EstVirementInterne"/>) sont exclus via
    /// <see cref="MouvementsExternes"/>.
    /// </summary>
    public async Task<IReadOnlyList<CategorieMontant>> RepartitionParCategorie(DateTime debut, DateTime fin, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var finExclusive = fin.Date.AddDays(1);

        var rows = await MouvementsExternes(db.Transactions.AsNoTracking())
            .Where(t => t.Type == TypeTransaction.Sortie
                && t.Date >= debut.Date
                && t.Date < finExclusive)
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

    /// <summary>Totaux d'entrées et de sorties par mois calendaire, du plus ancien au plus récent (hors virements internes).</summary>
    public Task<IReadOnlyList<MoisTotaux>> TotauxMensuels(int nombreDeMois, CancellationToken cancellationToken = default)
        => TotauxParMois(nombreDeMois, cancellationToken);

    public async Task<IReadOnlyList<MoisTotaux>> TotauxParMois(int nombreDeMois, CancellationToken cancellationToken = default)
    {
        if (nombreDeMois < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nombreDeMois));
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var today = DateTime.Today;
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(1 - nombreDeMois);
        var transactions = await MouvementsExternes(db.Transactions.AsNoTracking())
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

    /// <summary>
    /// Requête de base des mouvements « externes » (hors virements internes).
    /// À réutiliser pour camembert, totaux mensuels et moyenne de prévision.
    /// </summary>
    private static IQueryable<Transaction> MouvementsExternes(IQueryable<Transaction> query)
        => query.Where(t => !t.EstVirementInterne);

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

    public async Task ModifierCompteAsync(
        int id,
        string nom,
        decimal soldeInitial,
        TypeCompte type,
        CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom du compte est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var compte = await db.Comptes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Compte introuvable.");

        if (compte.Type == TypeCompte.Epargne
            && type != TypeCompte.Epargne
            && await db.ObjectifsEpargne.AnyAsync(o => o.CompteId == id, cancellationToken))
        {
            throw new InvalidOperationException("Ce compte a un objectif d'épargne. Supprimez l'objectif avant de le passer en compte courant.");
        }

        compte.Nom = trimmed;
        compte.SoldeInitial = decimal.Round(soldeInitial, 2);
        compte.Type = type;
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

        if (EstCategorieSysteme(categorie.Nom))
        {
            throw new InvalidOperationException("Cette catégorie système ne peut pas être modifiée.");
        }

        if (EstCategorieSysteme(trimmed))
        {
            throw new InvalidOperationException("Ce nom est réservé à une catégorie système.");
        }

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

    private static bool EstCategorieSysteme(string nom) =>
        string.Equals(nom, CategorieVirementInterne, StringComparison.Ordinal)
        || string.Equals(nom, CategorieNonCategorise, StringComparison.Ordinal);

    public async Task SupprimerCategorieAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var categorie = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categorie is null)
        {
            return;
        }

        if (EstCategorieSysteme(categorie.Nom))
        {
            throw new InvalidOperationException("Cette catégorie système ne peut pas être supprimée.");
        }

        var utilisee = await db.Transactions.CountAsync(t => t.Categorie == categorie.Nom, cancellationToken);
        if (utilisee > 0)
        {
            throw new CategorieEncoreUtiliseeException(categorie.Id, categorie.Nom, utilisee);
        }

        db.Categories.Remove(categorie);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Réassigne les transactions de la catégorie vers « Non catégorisé », puis supprime la catégorie.
    /// </summary>
    public async Task ReassignerEtSupprimerCategorieAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var categorie = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Catégorie introuvable.");

        if (EstCategorieSysteme(categorie.Nom))
        {
            throw new InvalidOperationException("Cette catégorie système ne peut pas être supprimée.");
        }

        var ancienNom = categorie.Nom;
        await EnsureCategorieCoreAsync(db, CategorieNonCategorise, "#6c757d", cancellationToken);

        await db.Transactions
            .Where(t => t.Categorie == ancienNom)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Categorie, CategorieNonCategorise), cancellationToken);

        // Recharger au cas où EnsureCategorie a déjà commit ; l'entité peut être détachée.
        var aSupprimer = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (aSupprimer is not null)
        {
            db.Categories.Remove(aSupprimer);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> CompterTransactionsParCategorieAsync(int categorieId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var categorie = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categorieId, cancellationToken);
        if (categorie is null)
        {
            return 0;
        }

        return await db.Transactions.CountAsync(t => t.Categorie == categorie.Nom, cancellationToken);
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
    /// Prévision du solde de fin de mois (estimation d'affichage uniquement, pas une garantie comptable) :
    /// <list type="number">
    /// <item>solde actuel total</item>
    /// <item>+ (moyenne quotidienne entrées − sorties) hors virements internes, sur N jours, × jours restants</item>
    /// <item>− total des <see cref="ChargeMensuelle"/> configurées sans transaction homonyme ce mois-ci
    /// (appariement par nom de charge + mois calendaire, sans lien FK)</item>
    /// </list>
    /// N'écrit aucune transaction. Les charges déjà « payées » (sortie dont la catégorie ou la note
    /// égale le nom de la charge, ou catégorie = nom) sont détectées approximativement via le nom
    /// de catégorie égal au nom de la charge.
    /// </summary>
    public async Task<PrevisionSoldeFinDeMois> PrevoirSoldeFinDeMoisAsync(CancellationToken cancellationToken = default)
    {
        var joursMoyenne = await GetJoursMoyennePrevisionAsync(cancellationToken);
        var today = DateTime.Today;
        var debutFenetre = today.AddDays(1 - joursMoyenne);
        var finExclusive = today.AddDays(1);
        var joursRestants = DateTime.DaysInMonth(today.Year, today.Month) - today.Day;
        var debutMois = new DateTime(today.Year, today.Month, 1);
        var finMoisExclusive = debutMois.AddMonths(1);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var mouvements = await MouvementsExternes(db.Transactions.AsNoTracking())
            .Where(t => t.Date >= debutFenetre && t.Date < finExclusive)
            .Select(t => new { t.Type, t.Montant })
            .ToListAsync(cancellationToken);

        var entrees = mouvements.Where(t => t.Type == TypeTransaction.Entree).Sum(t => t.Montant);
        var sorties = mouvements.Where(t => t.Type == TypeTransaction.Sortie).Sum(t => t.Montant);
        var moyenneEntrees = joursMoyenne == 0 ? 0 : entrees / joursMoyenne;
        var moyenneSorties = joursMoyenne == 0 ? 0 : sorties / joursMoyenne;

        var charges = await db.ChargesMensuelles.AsNoTracking().ToListAsync(cancellationToken);
        var categoriesPayeesCeMois = await MouvementsExternes(db.Transactions.AsNoTracking())
            .Where(t => t.Type == TypeTransaction.Sortie
                && t.Date >= debutMois
                && t.Date < finMoisExclusive)
            .Select(t => t.Categorie)
            .Distinct()
            .ToListAsync(cancellationToken);
        var payees = new HashSet<string>(categoriesPayeesCeMois, StringComparer.OrdinalIgnoreCase);

        var chargesRestantes = charges
            .Where(c => !payees.Contains(c.Nom))
            .Sum(c => c.Montant);

        var soldeActuel = await SoldeTotalAsync(cancellationToken);
        var prevision = soldeActuel
            + (moyenneEntrees - moyenneSorties) * joursRestants
            - chargesRestantes;

        return new PrevisionSoldeFinDeMois(
            decimal.Round(soldeActuel, 2),
            decimal.Round(prevision, 2),
            joursMoyenne,
            joursRestants,
            decimal.Round(moyenneEntrees, 2),
            decimal.Round(moyenneSorties, 2),
            decimal.Round(chargesRestantes, 2));
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

        if (string.Equals(transaction.Categorie.Trim(), CategorieVirementInterne, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Pour un virement interne, utilisez la fonction Virement (pas une transaction manuelle).");
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

/// <summary>Résultat de la prévision de fin de mois (estimation d'affichage, non comptable).</summary>
public sealed record PrevisionSoldeFinDeMois(
    decimal SoldeActuel,
    decimal Prevision,
    int JoursMoyenne,
    int JoursRestants,
    decimal MoyenneEntreesParJour,
    decimal MoyenneSortiesParJour,
    decimal ChargesMensuellesRestantes);

/// <summary>Levée lorsque la suppression d'une catégorie est bloquée car des transactions y font référence.</summary>
public sealed class CategorieEncoreUtiliseeException : InvalidOperationException
{
    public CategorieEncoreUtiliseeException(int categorieId, string nom, int nombreTransactions)
        : base($"{nombreTransactions} transaction(s) utilisent encore la catégorie « {nom} ». Réassignez-les vers « {FinanceService.CategorieNonCategorise} » ou annulez.")
    {
        CategorieId = categorieId;
        Nom = nom;
        NombreTransactions = nombreTransactions;
    }

    public int CategorieId { get; }

    public string Nom { get; }

    public int NombreTransactions { get; }
}

/// <summary>Progression vers l'objectif d'épargne actif (s'il existe).</summary>
public sealed record ProgressionObjectifEpargne(
    bool AUnObjectif,
    string? Nom,
    decimal MontantCible,
    decimal MontantActuel,
    decimal Pourcentage,
    DateTime? DateEstimee,
    bool EstimationDisponible,
    bool Atteint)
{
    public static ProgressionObjectifEpargne Aucun() =>
        new(false, null, 0, 0, 0, null, false, false);
}

/// <summary>Versements (entrées) sur les comptes épargne, mois en cours vs mois précédent.</summary>
public sealed record TendanceVersementsEpargne(
    decimal CeMois,
    decimal MoisPrecedent,
    decimal? VariationPourcent);
