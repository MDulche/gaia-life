using App.Modules.Stock.Data;
using App.Modules.Stock.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Stock.Services;

/// <summary>
/// Accès métier Stock. Chaque méthode ouvre son propre contexte via factory.
/// </summary>
public sealed class StockService
{
    private readonly IDbContextFactory<StockDbContext> _dbFactory;

    public StockService(IDbContextFactory<StockDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

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

    public async Task<int> CompterStockBasAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Articles.AsNoTracking()
            .CountAsync(a => a.SeuilAlerte != null && a.Quantite < a.SeuilAlerte, cancellationToken);
    }

    public async Task AjouterArticleAsync(
        string nom,
        decimal quantite,
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

        if (quantite < 0)
        {
            throw new InvalidOperationException("La quantité ne peut pas être négative.");
        }

        if (seuilAlerte is < 0)
        {
            throw new InvalidOperationException("Le seuil d'alerte ne peut pas être négatif.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Categories.AnyAsync(c => c.Id == categorieId, cancellationToken))
        {
            throw new InvalidOperationException("Catégorie introuvable.");
        }

        db.Articles.Add(new ArticleStock
        {
            Nom = nom,
            Quantite = quantite,
            Unite = string.IsNullOrWhiteSpace(unite) ? null : unite.Trim(),
            SeuilAlerte = seuilAlerte,
            CategorieId = categorieId,
            DateMaj = DateTime.Now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierArticleAsync(
        int id,
        string nom,
        decimal quantite,
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

        if (quantite < 0)
        {
            throw new InvalidOperationException("La quantité ne peut pas être négative.");
        }

        if (seuilAlerte is < 0)
        {
            throw new InvalidOperationException("Le seuil d'alerte ne peut pas être négatif.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Article introuvable.");

        if (!await db.Categories.AnyAsync(c => c.Id == categorieId, cancellationToken))
        {
            throw new InvalidOperationException("Catégorie introuvable.");
        }

        article.Nom = nom;
        article.Quantite = quantite;
        article.Unite = string.IsNullOrWhiteSpace(unite) ? null : unite.Trim();
        article.SeuilAlerte = seuilAlerte;
        article.CategorieId = categorieId;
        article.DateMaj = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
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
}
