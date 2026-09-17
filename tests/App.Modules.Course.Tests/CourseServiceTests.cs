using App.Modules.Course.Data;
using App.Modules.Course.Entities;
using App.Modules.Course.Services;
using App.Modules.Finance.Data;
using App.Modules.Finance.Entities;
using App.Modules.Finance.Liaisons;
using App.Modules.Finance.Services;
using App.Modules.Stock.Data;
using App.Modules.Stock.Entities;
using App.Modules.Stock.Liaisons;
using App.Modules.Stock.Services;
using App.Shared.Events;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Modules.Course.Tests;

public sealed class CourseServiceTests
{
    [Fact]
    public async Task MarquerAchete_deux_fois_true_publie_un_seul_evenement()
    {
        var bus = new RecordingBus();
        await using var harness = await CourseHarness.CreateAsync(bus);
        var articleId = await harness.SeedArticleAsync();

        await harness.Service.MarquerAchete(articleId, true);
        await harness.Service.MarquerAchete(articleId, true);

        Assert.Single(bus.Events);
        Assert.True((await harness.GetArticleAsync(articleId))!.Achete);
    }

    [Fact]
    public async Task SupprimerArticle_retire_la_ligne()
    {
        var bus = new RecordingBus();
        await using var harness = await CourseHarness.CreateAsync(bus);
        var articleId = await harness.SeedArticleAsync();

        await harness.Service.SupprimerArticle(articleId);

        Assert.Null(await harness.GetArticleAsync(articleId));
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
            // Décalage pour créer des trous (1..n attendu) sans violer l'unicité.
            await db.Database.ExecuteSqlRawAsync("UPDATE CourseMagasins SET Ordre = Ordre + 10");
            var bruts = await db.Magasins.AsNoTracking().Select(m => m.Ordre).ToListAsync();
            Assert.True(CourseService.ADesTrousOuDoublonsOrdre(bruts));
        }

        await harness.Service.NormaliserOrdreMagasinsAsync();

        var ordres = (await harness.Service.ListerMagasinsOrdonnes()).Select(m => m.Ordre).OrderBy(o => o).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, ordres);
    }

    [Fact]
    public async Task AjouterMagasin_concurrent_conserve_ordres_uniques()
    {
        await using var harness = await CourseHarness.CreateSqliteAsync(new RecordingBus());

        var tasks = Enumerable.Range(0, 8)
            .Select(i => harness.Service.AjouterMagasinAsync($"Magasin-{i}"))
            .ToArray();
        await Task.WhenAll(tasks);

        var magasins = await harness.Service.ListerMagasinsOrdonnes();
        Assert.Equal(8, magasins.Count);
        Assert.Equal(magasins.Count, magasins.Select(m => m.Ordre).Distinct().Count());
        Assert.False(CourseService.ADesTrousOuDoublonsOrdre(magasins.Select(m => m.Ordre)));
    }
}

public sealed class LiaisonCourseTests
{
    [Fact]
    public async Task Stock_liaison_active_avec_ArticleStockId_incremente()
    {
        await using var scope = await LiaisonHarness.CreateAsync(liaisonStock: true, liaisonFinance: false);
        var stockId = await scope.SeedStockArticleAsync(quantite: 2m);

        await scope.Bus.PublierAsync(new ArticleAcheteEvent(1, stockId, null, DateTime.Today));

        await using var db = await scope.StockFactory.CreateDbContextAsync();
        var article = await db.Articles.SingleAsync(a => a.Id == stockId);
        Assert.Equal(3m, article.Quantite);
    }

    [Fact]
    public async Task Stock_liaison_active_sans_ArticleStockId_aucun_effet()
    {
        await using var scope = await LiaisonHarness.CreateAsync(liaisonStock: true, liaisonFinance: false);
        var stockId = await scope.SeedStockArticleAsync(quantite: 2m);

        await scope.Bus.PublierAsync(new ArticleAcheteEvent(1, null, null, DateTime.Today));

        await using var db = await scope.StockFactory.CreateDbContextAsync();
        var article = await db.Articles.SingleAsync(a => a.Id == stockId);
        Assert.Equal(2m, article.Quantite);
    }

    [Fact]
    public async Task Finance_liaison_active_avec_prix_cree_sortie()
    {
        await using var scope = await LiaisonHarness.CreateAsync(liaisonStock: false, liaisonFinance: true);
        var compteId = await scope.SeedCompteAsync();
        scope.CompteCoursesParDefautId = compteId;

        await scope.Bus.PublierAsync(new ArticleAcheteEvent(42, null, 12.5m, DateTime.Today));

        await using var db = await scope.FinanceFactory.CreateDbContextAsync();
        var tx = await db.Transactions.SingleAsync();
        Assert.Equal(TypeTransaction.Sortie, tx.Type);
        Assert.Equal(12.5m, tx.Montant);
        Assert.Equal(ArticleAcheteFinanceSubscriber.CategorieCourses, tx.Categorie);
        Assert.Equal(compteId, tx.CompteId);
    }

    [Fact]
    public async Task Finance_liaison_active_sans_prix_aucun_effet()
    {
        await using var scope = await LiaisonHarness.CreateAsync(liaisonStock: false, liaisonFinance: true);
        var compteId = await scope.SeedCompteAsync();
        scope.CompteCoursesParDefautId = compteId;

        await scope.Bus.PublierAsync(new ArticleAcheteEvent(42, null, null, DateTime.Today));

        await using var db = await scope.FinanceFactory.CreateDbContextAsync();
        Assert.Empty(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task PublierAsync_handler_en_echec_n_empeche_pas_les_suivants()
    {
        var bus = new EvenementBus();
        var secondCalled = false;
        bus.Abonner<ArticleAcheteEvent>(_ => throw new InvalidOperationException("boom"));
        bus.Abonner<ArticleAcheteEvent>(_ =>
        {
            secondCalled = true;
            return Task.CompletedTask;
        });

        await bus.PublierAsync(new ArticleAcheteEvent(1, null, null, DateTime.Today));

        Assert.True(secondCalled);
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

file sealed class FakeLiaisonQuery(bool courseStock, bool courseFinance) : IModuleLiaisonQuery
{
    public Task<bool> IsLiaisonActiveAsync(string moduleA, string moduleB, CancellationToken cancellationToken = default)
    {
        var keys = new[] { moduleA.ToLowerInvariant(), moduleB.ToLowerInvariant() }.OrderBy(x => x).ToArray();
        var pair = string.Join('|', keys);
        return Task.FromResult(pair switch
        {
            "course|stock" => courseStock,
            "course|finance" => courseFinance,
            _ => false
        });
    }
}

file sealed class FakeCourseParametres(Func<int?> getter) : ICourseParametresQuery
{
    public Task<int?> GetCompteCoursesParDefautIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(getter());
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
        var services = new ServiceCollection();
        services.AddDbContextFactory<CourseDbContext>(o => o.UseSqlite($"Data Source={path}"));
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IDbContextFactory<CourseDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
        }

        return new CourseHarness(factory, new CourseService(factory, bus), path);
    }

    public async Task<int> SeedArticleAsync()
    {
        await using var db = await Factory.CreateDbContextAsync();
        var cat = new App.Modules.Course.Entities.Categorie { Nom = "Divers" };
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

file sealed class LiaisonHarness : IAsyncDisposable
{
    public EvenementBus Bus { get; }
    public IDbContextFactory<StockDbContext> StockFactory { get; }
    public IDbContextFactory<FinanceDbContext> FinanceFactory { get; }
    public int? CompteCoursesParDefautId { get; set; }

    private readonly string _stockPath;
    private readonly string _financePath;
    private readonly ServiceProvider _sp;

    private LiaisonHarness(
        EvenementBus bus,
        IDbContextFactory<StockDbContext> stockFactory,
        IDbContextFactory<FinanceDbContext> financeFactory,
        string stockPath,
        string financePath,
        ServiceProvider sp)
    {
        Bus = bus;
        StockFactory = stockFactory;
        FinanceFactory = financeFactory;
        _stockPath = stockPath;
        _financePath = financePath;
        _sp = sp;
    }

    public static async Task<LiaisonHarness> CreateAsync(bool liaisonStock, bool liaisonFinance)
    {
        var stockPath = Path.Combine(Path.GetTempPath(), $"stock-tests-{Guid.NewGuid():N}.db");
        var financePath = Path.Combine(Path.GetTempPath(), $"finance-tests-{Guid.NewGuid():N}.db");
        var bus = new EvenementBus();
        var holder = new CompteHolder();

        var services = new ServiceCollection();
        services.AddSingleton<IEvenementBus>(bus);
        services.AddSingleton<IModuleLiaisonQuery>(new FakeLiaisonQuery(liaisonStock, liaisonFinance));
        services.AddSingleton<ICourseParametresQuery>(new FakeCourseParametres(() => holder.Id));
        services.AddDbContextFactory<StockDbContext>(o => o.UseSqlite($"Data Source={stockPath}"));
        services.AddDbContextFactory<FinanceDbContext>(o => o.UseSqlite($"Data Source={financePath}"));
        services.AddScoped<StockService>();
        services.AddScoped<FinanceService>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        await using (var stockDb = await sp.GetRequiredService<IDbContextFactory<StockDbContext>>().CreateDbContextAsync())
        {
            await stockDb.Database.EnsureCreatedAsync();
        }

        await using (var financeDb = await sp.GetRequiredService<IDbContextFactory<FinanceDbContext>>().CreateDbContextAsync())
        {
            await financeDb.Database.EnsureCreatedAsync();
        }

        var stockSub = new ArticleAcheteStockSubscriber(
            bus,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ArticleAcheteStockSubscriber>.Instance);
        await stockSub.StartAsync(CancellationToken.None);

        var financeSub = new ArticleAcheteFinanceSubscriber(
            bus,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ArticleAcheteFinanceSubscriber>.Instance);
        await financeSub.StartAsync(CancellationToken.None);

        var harness = new LiaisonHarness(
            bus,
            sp.GetRequiredService<IDbContextFactory<StockDbContext>>(),
            sp.GetRequiredService<IDbContextFactory<FinanceDbContext>>(),
            stockPath,
            financePath,
            sp)
        {
            CompteCoursesParDefautId = null
        };

        // Synchronise le holder avec la propriété du harness.
        holder.Bind(() => harness.CompteCoursesParDefautId);
        return harness;
    }

    public async Task<int> SeedStockArticleAsync(decimal quantite)
    {
        await using var db = await StockFactory.CreateDbContextAsync();
        var cat = new App.Modules.Stock.Entities.Categorie { Nom = "Alim" };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        var article = new ArticleStock
        {
            Nom = "Lait",
            NomNormalise = "lait",
            Quantite = quantite,
            CategorieId = cat.Id,
            DateMaj = DateTime.UtcNow
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        if (quantite != 0)
        {
            db.Mouvements.Add(new MouvementStock
            {
                ArticleStockId = article.Id,
                Delta = quantite,
                Motif = MotifsMouvementStock.StockInitial,
                DateMouvement = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return article.Id;
    }

    public async Task<int> SeedCompteAsync()
    {
        await using var db = await FinanceFactory.CreateDbContextAsync();
        var compte = new Compte
        {
            Nom = "Courant",
            SoldeInitial = 100,
            Type = TypeCompte.Courant,
            EstPrincipal = true,
            DateCreation = DateTime.Now
        };
        db.Comptes.Add(compte);
        await db.SaveChangesAsync();
        return compte.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await _sp.DisposeAsync();
        TryDelete(_stockPath);
        TryDelete(_financePath);
    }

    private static void TryDelete(string path)
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

    private sealed class CompteHolder
    {
        private Func<int?> _getter = () => null;

        public int? Id => _getter();

        public void Bind(Func<int?> getter) => _getter = getter;
    }
}
