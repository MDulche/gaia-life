using App.Modules.Finance.Data;
using App.Modules.Finance.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Finance.Services;

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
        return await db.Comptes.AsNoTracking().OrderBy(c => c.Nom).ToListAsync(cancellationToken);
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
        var compte = new Compte
        {
            Nom = trimmed,
            SoldeInitial = soldeInitial,
            DateCreation = DateTime.Now
        };

        db.Comptes.Add(compte);
        await db.SaveChangesAsync(cancellationToken);
        return compte;
    }

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

public sealed record CategorieMontant(string Categorie, decimal Total, string? Couleur);

public sealed record MoisTotaux(DateTime Mois, decimal Entrees, decimal Sorties);
