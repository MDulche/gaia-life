using App.Modules.Course.Data;
using App.Modules.Course.Entities;
using App.Modules.Course.Services;
using App.Shared.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Modules.Course.Tests;

public sealed class CourseServiceTests
{
    [Fact]
    public async Task MarquerAcheteAsync_deux_fois_true_publie_un_seul_evenement()
    {
        var bus = new RecordingBus();
        await using var harness = await CourseHarness.CreateAsync(bus);
        var articleId = await harness.SeedArticleAsync();

        await harness.Service.MarquerAcheteAsync(articleId, true);
        await harness.Service.MarquerAcheteAsync(articleId, true);

        Assert.Single(bus.Events);
        Assert.True((await harness.GetArticleAsync(articleId))!.Achete);
    }

    [Fact]
    public async Task SupprimerArticleAsync_retire_la_ligne()
    {
        var bus = new RecordingBus();
        await using var harness = await CourseHarness.CreateAsync(bus);
        var articleId = await harness.SeedArticleAsync();

        await harness.Service.SupprimerArticleAsync(articleId);

        Assert.Null(await harness.GetArticleAsync(articleId));
    }

    [Fact]
    public async Task AjouterArticleAsync_persiste_et_apparait_dans_la_liste()
    {
        await using var harness = await CourseHarness.CreateSqliteAsync(new RecordingBus());
        await using (var db = await harness.Factory.CreateDbContextAsync())
        {
            db.Categories.Add(new Categorie { Nom = "Frais" });
            await db.SaveChangesAsync();
        }

        var catId = (await harness.Service.ListerCategoriesAsync())[0].Id;
        await harness.Service.AjouterArticleAsync(new ArticleCourse
        {
            Nom = "Beurre",
            CategorieId = catId,
            Quantite = "250g"
        });

        var liste = await harness.Service.ListerArticlesAEnAcheterAsync();
        Assert.Contains(liste, a => a.Nom == "Beurre" && a.Quantite == "250g");
    }

    [Fact]
    public async Task RepartitionParCategorieAsync_compte_les_achats_du_mois()
    {
        var bus = new RecordingBus();
        await using var harness = await CourseHarness.CreateSqliteAsync(bus);
        var articleId = await harness.SeedArticleAsync();

        await harness.Service.MarquerAcheteAsync(articleId, true);

        var parts = await harness.Service.RepartitionParCategorieAsync();
        Assert.Single(parts);
        Assert.Equal("Divers", parts[0].Categorie);
        Assert.Equal(1, parts[0].Nombre);
    }

    [Fact]
    public async Task Sqlite_persiste_apres_reouverture_du_fichier()
    {
        var path = Path.Combine(Path.GetTempPath(), $"course-persist-{Guid.NewGuid():N}.db");
        try
        {
            await using (var harness = await CourseHarness.CreateSqliteAtPathAsync(new RecordingBus(), path, deleteOnDispose: false))
            {
                var id = await harness.SeedArticleAsync();
                await harness.Service.MarquerAcheteAsync(id, true);
            }

            await using var reopen = await CourseHarness.CreateSqliteAtPathAsync(new RecordingBus(), path, deleteOnDispose: false);
            var achetes = await reopen.Service.ListerArticlesAchetesAsync();
            Assert.Single(achetes);
            Assert.Equal("Lait", achetes[0].Nom);
            Assert.True(achetes[0].Achete);
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public async Task Ordre_magasins_doublon_simule_puis_normalise()
    {
        await using var harness = await CourseHarness.CreateSqliteAsync(new RecordingBus());
        await harness.Service.AjouterMagasinAsync("Alpha");
        await harness.Service.AjouterMagasinAsync("Beta");
        await harness.Service.AjouterMagasinAsync("Gamma");

        await using (var db = await harness.Factory.CreateDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE CourseMagasins SET Ordre = Ordre + 10");
            var bruts = await db.Magasins.AsNoTracking().Select(m => m.Ordre).ToListAsync();
            Assert.True(CourseService.ADesTrousOuDoublonsOrdre(bruts));
        }

        await harness.Service.NormaliserOrdreMagasinsAsync();

        var ordres = (await harness.Service.ListerMagasinsOrdonnesAsync()).Select(m => m.Ordre).OrderBy(o => o).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, ordres);
    }

    [Fact]
    public async Task PublierAsync_sans_abonne_ne_leve_pas()
    {
        var bus = new EvenementBus();
        await bus.PublierAsync(new ArticleAcheteEvent(1, null, 3.5m, DateTime.Now));
    }
}

file sealed class RecordingBus : IEvenementBus
{
    public List<object> Events { get; } = [];
    private readonly List<Delegate> _handlers = [];

    public async Task PublierAsync<T>(T evenement, CancellationToken cancellationToken = default)
    {
        Events.Add(evenement!);
        foreach (var handler in _handlers.OfType<Func<T, Task>>())
        {
            await handler(evenement);
        }
    }

    public void Abonner<T>(Func<T, Task> handler) => _handlers.Add(handler);
}

file sealed class CourseHarness : IAsyncDisposable
{
    public IDbContextFactory<CourseDbContext> Factory { get; }
    public CourseService Service { get; }
    private readonly string? _sqlitePath;

    private CourseHarness(IDbContextFactory<CourseDbContext> factory, CourseService service, string? sqlitePath)
    {
        Factory = factory;
        Service = service;
        _sqlitePath = sqlitePath;
    }

    public static async Task<CourseHarness> CreateAsync(IEvenementBus bus)
    {
        var name = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContextFactory<CourseDbContext>(o => o.UseInMemoryDatabase(name));
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IDbContextFactory<CourseDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        return new CourseHarness(factory, new CourseService(factory, bus), null);
    }

    public static async Task<CourseHarness> CreateSqliteAsync(IEvenementBus bus)
    {
        var path = Path.Combine(Path.GetTempPath(), $"course-tests-{Guid.NewGuid():N}.db");
        return await CreateSqliteAtPathAsync(bus, path, deleteOnDispose: true);
    }

    public static async Task<CourseHarness> CreateSqliteAtPathAsync(
        IEvenementBus bus,
        string path,
        bool deleteOnDispose = true)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<CourseDbContext>(o => o.UseSqlite($"Data Source={path}"));
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IDbContextFactory<CourseDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        return new CourseHarness(factory, new CourseService(factory, bus), deleteOnDispose ? path : null);
    }

    public async Task<int> SeedArticleAsync()
    {
        await using var db = await Factory.CreateDbContextAsync();
        var cat = new Categorie { Nom = "Divers" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        var article = new ArticleCourse
        {
            Nom = "Lait",
            CategorieId = cat.Id,
            Achete = false,
            DateAjout = DateTime.Now
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article.Id;
    }

    public async Task<ArticleCourse?> GetArticleAsync(int id)
    {
        await using var db = await Factory.CreateDbContextAsync();
        return await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    public ValueTask DisposeAsync()
    {
        if (_sqlitePath is not null && File.Exists(_sqlitePath))
        {
            try
            {
                File.Delete(_sqlitePath);
            }
            catch
            {
                // ignore lock on CI
            }
        }

        return ValueTask.CompletedTask;
    }
}
