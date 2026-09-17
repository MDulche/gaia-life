using App.Modules.Stock.Data;
using App.Modules.Stock.Entities;
using App.Modules.Stock.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Stock.Tests;

internal sealed class StockTestFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public IDbContextFactory<StockDbContext> Factory { get; }

    public StockService Service { get; }

    private StockTestFixture(SqliteConnection connection, IDbContextFactory<StockDbContext> factory)
    {
        _connection = connection;
        Factory = factory;
        Service = new StockService(factory);
    }

    public static async Task<StockTestFixture> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<StockDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var db = new StockDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var factory = new TestDbContextFactory(options);
        return new StockTestFixture(connection, factory);
    }

    public async Task<int> SeedCategorieAsync(string nom = "Alimentaire")
    {
        await using var db = await Factory.CreateDbContextAsync();
        var cat = new Categorie { Nom = nom };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return cat.Id;
    }

    public async Task<int> SeedArticleAsync(int categorieId, string nom, decimal quantite)
    {
        var resultat = await Service.AjouterArticleAsync(nom, quantite, null, null, categorieId);
        return resultat.ArticleId;
    }

    public async Task<decimal> GetQuantiteAsync(int articleId)
    {
        await using var db = await Factory.CreateDbContextAsync();
        return await db.Articles.Where(a => a.Id == articleId).Select(a => a.Quantite).SingleAsync();
    }

    public async Task<int> CompterMouvementsAsync(int articleId)
    {
        await using var db = await Factory.CreateDbContextAsync();
        return await db.Mouvements.CountAsync(m => m.ArticleStockId == articleId);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<StockDbContext> options)
        : IDbContextFactory<StockDbContext>
    {
        public StockDbContext CreateDbContext() => new(options);

        public ValueTask<StockDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new StockDbContext(options));
    }
}
