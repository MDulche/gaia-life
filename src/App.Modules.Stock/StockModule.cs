using App.Modules.Stock.Components;
using App.Modules.Stock.Data;
using App.Modules.Stock.Liaisons;
using App.Modules.Stock.Services;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Modules.Stock;

/// <summary>Module Stock : factory DbContext SQLite, service, widget d'accueil, liaison Courses.</summary>
public sealed class StockModule : IAppModule
{
    public const string ModuleKey = "stock";

    public string Key => ModuleKey;

    public string DisplayName => "Stock";

    public string Icon => "bi-box-seam";

    public string RootRoute => "/stock";

    public Type? HomeWidgetComponent => typeof(StockHomeWidget);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContextFactory<StockDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("La chaîne de connexion 'Default' (SQLite) est introuvable.");
            options.UseStockSqlite(connectionString);
        });
        services.AddScoped<StockService>();
        services.AddScoped<IStockArticleCatalogue, StockArticleCatalogue>();
        services.AddSingleton<ArticleAcheteStockSubscriber>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ArticleAcheteStockSubscriber>());
    }

    /// <summary>No-op si le module n'est pas enregistré (factory absente du DI).</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetService<IDbContextFactory<StockDbContext>>();
        if (factory is null)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
