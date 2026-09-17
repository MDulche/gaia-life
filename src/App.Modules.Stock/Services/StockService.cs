using App.Modules.Stock.Data;
using App.Modules.Stock.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Stock.Services;

/// <summary>
/// Accès métier Stock. Chaque méthode ouvre son propre contexte via factory.
/// Toute modification de quantité passe par <see cref="AjusterQuantiteAsync"/>.
/// </summary>
public sealed class StockService
{
    private readonly IDbContextFactory<StockDbContext> _dbFactory;

    public StockService(IDbContextFactory<StockDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public static string NormaliserNom(string nom) => nom.Trim().ToLowerInvariant();

    public async Task<List<ArticleStock>> ListerArticlesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Articles.AsNoTracking()
            .Include(a => a.Categorie)
            .OrderBy(a => a.Nom)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Categorie>> ListerCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking()
            .OrderBy(c => c.Nom)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MouvementStock>> ListerMouvementsAsync(
        int? articleStockId = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Mouvements.AsNoTracking().AsQueryable();
        if (articleStockId is int id)
        {
            query = query.Where(m => m.ArticleStockId == id);
        }

        return await query
            .OrderByDescending(m => m.DateMouvement)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CompterStockBasAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Articles.AsNoTracking()
            .CountAsync(a => a.SeuilAlerte != null && a.Quantite < a.SeuilAlerte, cancellationToken);
    }

    /// <summary>
    /// Point d'entrée unique pour toute modification de quantité.
    /// Insère un <see cref="MouvementStock"/> puis recalcule <see cref="ArticleStock.Quantite"/>
    /// comme somme des deltas, dans une transaction avec verrou ligne.
    /// </summary>
    public async Task<decimal> AjusterQuantiteAsync(
        int articleStockId,
        decimal delta,
        string motif,
        CancellationToken cancellationToken = default)
    {
        motif = motif?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(motif))
        {
            throw new InvalidOperationException("Le motif du mouvement est obligatoire.");
        }

        if (motif.Length > 128)
        {
            throw new InvalidOperationException("Le motif du mouvement est trop long.");
        }

        if (delta == 0)
        {
            await using var dbRead = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var q = await dbRead.Articles.AsNoTracking()
                .Where(a => a.Id == articleStockId)
                .Select(a => (decimal?)a.Quantite)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Article introuvable.");
            return q;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        await VerrouillerArticleAsync(db, articleStockId, cancellationToken);

        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == articleStockId, cancellationToken)
            ?? throw new InvalidOperationException("Article introuvable.");

        db.Mouvements.Add(new MouvementStock
        {
            ArticleStockId = articleStockId,
            Delta = delta,
            Motif = motif,
            DateMouvement = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        var quantite = await db.Mouvements
            .Where(m => m.ArticleStockId == articleStockId)
            .SumAsync(m => m.Delta, cancellationToken);

        if (quantite < 0 && !MotifsMouvementStock.AutoriseQuantiteNegative(motif))
        {
            throw new InvalidOperationException(
                "La quantité ne peut pas devenir négative (sauf motif « Ajustement inventaire »).");
        }

        article.Quantite = quantite;
        article.DateMaj = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return quantite;
    }

    /// <summary>
    /// Crée un article, ou propose / exécute une fusion si le nom normalisé existe déjà.
    /// </summary>
    public async Task<AjoutArticleResultat> AjouterArticleAsync(
        string nom,
        decimal quantite,
        string? unite,
        decimal? seuilAlerte,
        int categorieId,
        bool fusionnerSiExistant = false,
        CancellationToken cancellationToken = default)
    {
        nom = nom.Trim();
        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new InvalidOperationException("Le nom de l'article est obligatoire.");
        }

        if (nom.Length > 128)
        {
            throw new InvalidOperationException("Le nom de l'article est trop long.");
        }

        if (quantite < 0)
        {
            throw new InvalidOperationException("La quantité ne peut pas être négative.");
        }

        if (seuilAlerte is < 0)
        {
            throw new InvalidOperationException("Le seuil d'alerte ne peut pas être négatif.");
        }

        var nomNormalise = NormaliserNom(nom);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Categories.AnyAsync(c => c.Id == categorieId, cancellationToken))
        {
            throw new InvalidOperationException("Catégorie introuvable.");
        }

        var existant = await db.Articles.AsNoTracking()
            .FirstOrDefaultAsync(a => a.NomNormalise == nomNormalise, cancellationToken);

        if (existant is not null)
        {
            if (!fusionnerSiExistant)
            {
                return AjoutArticleResultat.ProposerFusion(existant.Id, existant.Nom);
            }

            if (quantite != 0)
            {
                await AjusterQuantiteAsync(
                    existant.Id,
                    quantite,
                    MotifsMouvementStock.CorrectionManuelle,
                    cancellationToken);
            }

            return AjoutArticleResultat.Fusionne(existant.Id);
        }

        var article = new ArticleStock
        {
            Nom = nom,
            NomNormalise = nomNormalise,
            Quantite = 0,
            Unite = string.IsNullOrWhiteSpace(unite) ? null : unite.Trim(),
            SeuilAlerte = seuilAlerte,
            CategorieId = categorieId,
            DateMaj = DateTime.UtcNow
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync(cancellationToken);

        if (quantite != 0)
        {
            await AjusterQuantiteAsync(
                article.Id,
                quantite,
                MotifsMouvementStock.StockInitial,
                cancellationToken);
        }

        return AjoutArticleResultat.CreeNouveau(article.Id);
    }

    /// <summary>Met à jour les infos article sans toucher à la quantité.</summary>
    public async Task ModifierInfosArticleAsync(
        int id,
        string nom,
        string? unite,
        decimal? seuilAlerte,
        int categorieId,
        CancellationToken cancellationToken = default)
    {
        nom = nom.Trim();
        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new InvalidOperationException("Le nom de l'article est obligatoire.");
        }

        if (nom.Length > 128)
        {
            throw new InvalidOperationException("Le nom de l'article est trop long.");
        }

        if (seuilAlerte is < 0)
        {
            throw new InvalidOperationException("Le seuil d'alerte ne peut pas être négatif.");
        }

        var nomNormalise = NormaliserNom(nom);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Article introuvable.");

        if (!await db.Categories.AnyAsync(c => c.Id == categorieId, cancellationToken))
        {
            throw new InvalidOperationException("Catégorie introuvable.");
        }

        if (await db.Articles.AnyAsync(a => a.Id != id && a.NomNormalise == nomNormalise, cancellationToken))
        {
            throw new InvalidOperationException("Un article avec ce nom existe déjà.");
        }

        article.Nom = nom;
        article.NomNormalise = nomNormalise;
        article.Unite = string.IsNullOrWhiteSpace(unite) ? null : unite.Trim();
        article.SeuilAlerte = seuilAlerte;
        article.CategorieId = categorieId;
        article.DateMaj = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ajuste la quantité pour atteindre une valeur cible (inventaire), via <see cref="AjusterQuantiteAsync"/>.
    /// </summary>
    public async Task DefinirQuantiteInventaireAsync(
        int articleStockId,
        decimal quantiteCible,
        CancellationToken cancellationToken = default)
    {
        if (quantiteCible < 0)
        {
            throw new InvalidOperationException("La quantité cible ne peut pas être négative.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var actuelle = await db.Articles.AsNoTracking()
            .Where(a => a.Id == articleStockId)
            .Select(a => (decimal?)a.Quantite)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Article introuvable.");

        var delta = quantiteCible - actuelle;
        if (delta == 0)
        {
            return;
        }

        await AjusterQuantiteAsync(
            articleStockId,
            delta,
            MotifsMouvementStock.AjustementInventaire,
            cancellationToken);
    }

    public async Task SupprimerArticleAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return;
        }

        db.Articles.Remove(article);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AjouterCategorieAsync(string nom, string? couleur, CancellationToken cancellationToken = default)
    {
        nom = nom.Trim();
        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new InvalidOperationException("Le nom de la catégorie est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Categories.AnyAsync(c => c.Nom == nom, cancellationToken))
        {
            throw new InvalidOperationException("Une catégorie avec ce nom existe déjà.");
        }

        db.Categories.Add(new Categorie
        {
            Nom = nom,
            Couleur = string.IsNullOrWhiteSpace(couleur) ? null : couleur.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierCategorieAsync(int id, string nom, string? couleur, CancellationToken cancellationToken = default)
    {
        nom = nom.Trim();
        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new InvalidOperationException("Le nom de la catégorie est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var categorie = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Catégorie introuvable.");

        if (await db.Categories.AnyAsync(c => c.Id != id && c.Nom == nom, cancellationToken))
        {
            throw new InvalidOperationException("Une catégorie avec ce nom existe déjà.");
        }

        categorie.Nom = nom;
        categorie.Couleur = string.IsNullOrWhiteSpace(couleur) ? null : couleur.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerCategorieAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Articles.AnyAsync(a => a.CategorieId == id, cancellationToken))
        {
            throw new InvalidOperationException("Impossible de supprimer une catégorie encore utilisée par des articles.");
        }

        var categorie = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categorie is null)
        {
            return;
        }

        db.Categories.Remove(categorie);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static bool EstStockBas(ArticleStock article) =>
        article.SeuilAlerte is { } seuil && article.Quantite < seuil;

    private static async Task VerrouillerArticleAsync(
        StockDbContext db,
        int articleStockId,
        CancellationToken cancellationToken)
    {
        // MariaDB / MySQL : verrou exclusif sur la ligne. SQLite ignore FOR UPDATE (sérialisation fichier).
        var provider = db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT Id FROM StockArticles WHERE Id = {articleStockId} FOR UPDATE",
            cancellationToken);
    }
}
