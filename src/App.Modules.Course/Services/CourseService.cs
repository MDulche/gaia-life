using App.Modules.Course.Data;
using App.Modules.Course.Entities;
using App.Shared.Events;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Course.Services;

/// <summary>
/// Accès métier Courses. Chaque méthode ouvre son propre contexte via factory.
/// </summary>
public sealed class CourseService : ICourseParametresQuery
{
    private readonly IDbContextFactory<CourseDbContext> _dbFactory;
    private readonly IEvenementBus _evenements;

    public CourseService(IDbContextFactory<CourseDbContext> dbFactory, IEvenementBus evenements)
    {
        _dbFactory = dbFactory;
        _evenements = evenements;
    }

    public async Task<List<ArticleCourse>> ListerArticlesAEnAcheterAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Articles.AsNoTracking()
            .Include(a => a.Categorie)
            .Include(a => a.Magasin)
            .Where(a => !a.Achete)
            .OrderBy(a => a.Magasin == null ? int.MaxValue : a.Magasin.Ordre)
            .ThenBy(a => a.Nom)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ArticleCourse>> ListerArticlesAchetesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Articles.AsNoTracking()
            .Include(a => a.Categorie)
            .Include(a => a.Magasin)
            .Where(a => a.Achete)
            .OrderByDescending(a => a.DateAchat)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Force l'état acheté. Publie <see cref="ArticleAcheteEvent"/> uniquement sur le passage false → true.
    /// </summary>
    public async Task MarquerAcheteAsync(int articleId, bool valeur, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.Articles.FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken)
            ?? throw new InvalidOperationException("Article introuvable.");

        if (article.Achete == valeur)
        {
            return;
        }

        var etaitAchete = article.Achete;
        article.Achete = valeur;
        article.DateAchat = valeur ? DateTime.Now : null;
        await db.SaveChangesAsync(cancellationToken);

        if (!etaitAchete && valeur)
        {
            await _evenements.PublierAsync(new ArticleAcheteEvent(
                article.Id,
                article.ArticleStockId,
                article.PrixEstime,
                article.DateAchat ?? DateTime.Now), cancellationToken);
        }
    }

    public async Task AjouterArticleAsync(ArticleCourse article, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);
        var nom = article.Nom.Trim();
        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new InvalidOperationException("Le nom de l'article est obligatoire.");
        }

        if (article.PrixEstime is < 0)
        {
            throw new InvalidOperationException("Le prix estimé ne peut pas être négatif.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Categories.AnyAsync(c => c.Id == article.CategorieId, cancellationToken))
        {
            throw new InvalidOperationException("Catégorie introuvable.");
        }

        if (article.MagasinId is { } magasinId && !await db.Magasins.AnyAsync(m => m.Id == magasinId, cancellationToken))
        {
            throw new InvalidOperationException("Magasin introuvable.");
        }

        db.Articles.Add(new ArticleCourse
        {
            Nom = nom,
            CategorieId = article.CategorieId,
            MagasinId = article.MagasinId,
            Quantite = string.IsNullOrWhiteSpace(article.Quantite) ? null : article.Quantite.Trim(),
            ArticleStockId = article.ArticleStockId,
            PrixEstime = article.PrixEstime,
            Achete = false,
            DateAjout = DateTime.Now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ModifierArticleAsync(ArticleCourse article, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);
        var nom = article.Nom.Trim();
        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new InvalidOperationException("Le nom de l'article est obligatoire.");
        }

        if (article.PrixEstime is < 0)
        {
            throw new InvalidOperationException("Le prix estimé ne peut pas être négatif.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Articles.FirstOrDefaultAsync(a => a.Id == article.Id, cancellationToken)
            ?? throw new InvalidOperationException("Article introuvable.");

        if (!await db.Categories.AnyAsync(c => c.Id == article.CategorieId, cancellationToken))
        {
            throw new InvalidOperationException("Catégorie introuvable.");
        }

        if (article.MagasinId is { } magasinId && !await db.Magasins.AnyAsync(m => m.Id == magasinId, cancellationToken))
        {
            throw new InvalidOperationException("Magasin introuvable.");
        }

        existing.Nom = nom;
        existing.CategorieId = article.CategorieId;
        existing.MagasinId = article.MagasinId;
        existing.Quantite = string.IsNullOrWhiteSpace(article.Quantite) ? null : article.Quantite.Trim();
        existing.ArticleStockId = article.ArticleStockId;
        existing.PrixEstime = article.PrixEstime;
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

    public async Task<IReadOnlyList<CourseCategoriePart>> RepartitionParCategorieAsync(CancellationToken cancellationToken = default)
    {
        var debut = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var finExclusive = debut.AddMonths(1);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Articles.AsNoTracking()
            .Where(a => a.Achete && a.DateAchat != null && a.DateAchat >= debut && a.DateAchat < finExclusive)
            .GroupBy(a => a.CategorieId)
            .Select(g => new { CategorieId = g.Key, Nombre = g.Count() })
            .ToListAsync(cancellationToken);

        var couleurs = await db.Categories.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => (c.Nom, c.Couleur), cancellationToken);

        return rows
            .Select(r =>
            {
                var info = couleurs.GetValueOrDefault(r.CategorieId);
                return new CourseCategoriePart(info.Nom ?? $"#{r.CategorieId}", r.Nombre, info.Couleur);
            })
            .OrderByDescending(r => r.Nombre)
            .ToList();
    }

    public async Task<List<Magasin>> ListerMagasinsOrdonnesAsync(CancellationToken cancellationToken = default)
    {
        await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var magasins = await db.Magasins.AsNoTracking()
                .OrderBy(m => m.Ordre)
                .ThenBy(m => m.Nom)
                .ToListAsync(cancellationToken);

            if (!ADesTrousOuDoublonsOrdre(magasins.Select(m => m.Ordre)))
            {
                return magasins;
            }
        }

        await NormaliserOrdreMagasinsAsync(cancellationToken);
        await using var dbFresh = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await dbFresh.Magasins.AsNoTracking()
            .OrderBy(m => m.Ordre)
            .ThenBy(m => m.Nom)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MagasinRestants>> ListerMagasinsAvecRestantsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var magasins = await db.Magasins.AsNoTracking()
            .OrderBy(m => m.Ordre)
            .ThenBy(m => m.Nom)
            .ToListAsync(cancellationToken);
        var counts = await db.Articles.AsNoTracking()
            .Where(a => !a.Achete && a.MagasinId != null)
            .GroupBy(a => a.MagasinId!.Value)
            .Select(g => new { MagasinId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MagasinId, g => g.Count, cancellationToken);

        return magasins
            .Select(m => new MagasinRestants(m.Id, m.Nom, m.Ordre, counts.GetValueOrDefault(m.Id)))
            .ToList();
    }

    public async Task<CourseResumeAccueil> ResumeAccueilAsync(CancellationToken cancellationToken = default)
    {
        var restants = await ListerArticlesAEnAcheterAsync(cancellationToken);
        var prochain = restants
            .Where(a => a.Magasin is not null)
            .Select(a => a.Magasin!)
            .DistinctBy(m => m.Id)
            .OrderBy(m => m.Ordre)
            .FirstOrDefault();

        return new CourseResumeAccueil(restants.Count, prochain?.Nom);
    }

    public async Task<List<Categorie>> ListerCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking().OrderBy(c => c.Nom).ToListAsync(cancellationToken);
    }

    public async Task AjouterMagasinAsync(string nom, CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom du magasin est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var maxOrdre = await db.Magasins.Select(m => (int?)m.Ordre).MaxAsync(cancellationToken) ?? 0;
            db.Magasins.Add(new Magasin { Nom = trimmed, Ordre = maxOrdre + 1 });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            // Collision concurrente sur Ordre : normaliser puis réessayer une fois.
            await NormaliserOrdreMagasinsAsync(cancellationToken);
            await using var dbRetry = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var maxOrdre = await dbRetry.Magasins.Select(m => (int?)m.Ordre).MaxAsync(cancellationToken) ?? 0;
            dbRetry.Magasins.Add(new Magasin { Nom = trimmed, Ordre = maxOrdre + 1 });
            await dbRetry.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ModifierMagasinAsync(int id, string nom, CancellationToken cancellationToken = default)
    {
        var trimmed = nom.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Le nom du magasin est obligatoire.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var magasin = await db.Magasins.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Magasin introuvable.");
        magasin.Nom = trimmed;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerMagasinAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var magasin = await db.Magasins.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (magasin is null)
        {
            return;
        }

        db.Magasins.Remove(magasin);
        await db.SaveChangesAsync(cancellationToken);
        await NormaliserOrdreMagasinsAsync(cancellationToken);
    }

    public async Task DeplacerMagasinAsync(int id, int delta, CancellationToken cancellationToken = default)
    {
        if (delta is not (1 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var magasins = await db.Magasins.OrderBy(m => m.Ordre).ThenBy(m => m.Nom).ToListAsync(cancellationToken);
            if (ADesTrousOuDoublonsOrdre(magasins.Select(m => m.Ordre)))
            {
                await NormaliserOrdreMagasinsCoreAsync(db, cancellationToken);
                magasins = await db.Magasins.OrderBy(m => m.Ordre).ThenBy(m => m.Nom).ToListAsync(cancellationToken);
            }

            var index = magasins.FindIndex(m => m.Id == id);
            if (index < 0)
            {
                throw new InvalidOperationException("Magasin introuvable.");
            }

            var cible = index + delta;
            if (cible < 0 || cible >= magasins.Count)
            {
                await tx.CommitAsync(cancellationToken);
                return;
            }

            (magasins[index].Ordre, magasins[cible].Ordre) = (magasins[cible].Ordre, magasins[index].Ordre);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Ré-numérote les magasins 1..n sans trou ni doublon.</summary>
    public async Task NormaliserOrdreMagasinsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await NormaliserOrdreMagasinsCoreAsync(db, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
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
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Nom == trimmed, cancellationToken))
        {
            throw new InvalidOperationException("Une catégorie porte déjà ce nom.");
        }

        categorie.Nom = trimmed;
        categorie.Couleur = string.IsNullOrWhiteSpace(couleur) ? null : couleur.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SupprimerCategorieAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Articles.AnyAsync(a => a.CategorieId == id, cancellationToken))
        {
            throw new InvalidOperationException("Cette catégorie a des articles associés. Supprimez-les d'abord.");
        }

        var categorie = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categorie is null)
        {
            return;
        }

        db.Categories.Remove(categorie);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ViderHistoriqueAchetesAsync(DateTime avant, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var limite = avant.Date;
        var aSupprimer = await db.Articles
            .Where(a => a.Achete && a.DateAchat != null && a.DateAchat < limite)
            .ToListAsync(cancellationToken);
        db.Articles.RemoveRange(aSupprimer);
        await db.SaveChangesAsync(cancellationToken);
        return aSupprimer.Count;
    }

    public async Task<int?> GetCompteCoursesParDefautIdAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var parametre = await db.Parametres.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);
        return parametre?.CompteCoursesParDefautId;
    }

    public async Task DefinirCompteCoursesParDefautAsync(int? compteId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var parametre = await db.Parametres.OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);
        if (parametre is null)
        {
            parametre = new CourseParametre();
            db.Parametres.Add(parametre);
        }

        parametre.CompteCoursesParDefautId = compteId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static bool ADesTrousOuDoublonsOrdre(IEnumerable<int> ordres)
    {
        var list = ordres.OrderBy(o => o).ToList();
        if (list.Count == 0)
        {
            return false;
        }

        if (list.Distinct().Count() != list.Count)
        {
            return true;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] != i + 1)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task NormaliserOrdreMagasinsCoreAsync(CourseDbContext db, CancellationToken cancellationToken)
    {
        var magasins = await db.Magasins.OrderBy(m => m.Ordre).ThenBy(m => m.Nom).ThenBy(m => m.Id).ToListAsync(cancellationToken);
        // Deux passes pour éviter les collisions sur l'index unique pendant la renumérotation.
        for (var i = 0; i < magasins.Count; i++)
        {
            magasins[i].Ordre = -(i + 1);
        }

        await db.SaveChangesAsync(cancellationToken);

        for (var i = 0; i < magasins.Count; i++)
        {
            magasins[i].Ordre = i + 1;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Part du camembert (achats du mois, par catégorie).</summary>
public sealed record CourseCategoriePart(string Categorie, int Nombre, string? Couleur);

/// <summary>Magasin du parcours avec le nombre d'articles encore à acheter.</summary>
public sealed record MagasinRestants(int Id, string Nom, int Ordre, int Restants);

/// <summary>Résumé pour le widget d'accueil.</summary>
public sealed record CourseResumeAccueil(int ArticlesRestants, string? ProchainMagasin);
