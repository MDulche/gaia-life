using App.Modules.Finance.Components;
using App.Modules.Finance.Data;
using App.Modules.Finance.Liaisons;
using App.Modules.Finance.Services;
using App.Shared.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.Modules.Finance;

/// <summary>Module Finances : factory DbContext SQLite, service, liaison Courses.</summary>
public sealed class FinanceModule : IAppModule
{
    public const string ModuleKey = "finance";

    public string Key => ModuleKey;

    public string DisplayName => "Finances";

    public string Icon => "bi-cash-coin";

    public string RootRoute => "/finance";

    public Type? HomeWidgetComponent => typeof(FinanceHomeWidget);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContextFactory<FinanceDbContext>((sp, options) =>
        {
            options.UseSqlite(ResolveConnectionString(sp));
        });

        // Factory compagnon pour MigrateAsync SQLite (migrations dans Migrations/Sqlite/).
        services.AddDbContextFactory<FinanceSqliteDbContext>((sp, options) =>
        {
            options.UseSqlite(ResolveConnectionString(sp));
        });

        services.AddScoped<FinanceService>();
        services.AddScoped<IFinanceCompteCatalogue, FinanceCompteCatalogue>();
        services.TryAddSingleton<IModuleLiaisonQuery, NullModuleLiaisonQuery>();
        services.TryAddSingleton<ICourseParametresQuery, NullCourseParametresQuery>();
        services.AddSingleton<ArticleAcheteFinanceSubscriber>();
        services.AddHostedService(sp => sp.GetRequiredService<ArticleAcheteFinanceSubscriber>());
    }

    /// <summary>Applique les migrations SQLite via <see cref="FinanceSqliteDbContext"/>.</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var sqliteFactory = sp.GetService<IDbContextFactory<FinanceSqliteDbContext>>();
        if (sqliteFactory is null)
        {
            return;
        }

        await using var db = await sqliteFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static string ResolveConnectionString(IServiceProvider sp) =>
        TryResolveConnectionString(sp)
        ?? throw new InvalidOperationException("La chaîne de connexion 'Default' (SQLite) est introuvable.");

    private static string? TryResolveConnectionString(IServiceProvider sp)
    {
        var sqlite = sp.GetService<GaiaSqliteOptions>();
        if (sqlite is not null && !string.IsNullOrWhiteSpace(sqlite.ConnectionString))
        {
            return sqlite.ConnectionString;
        }

        return sp.GetService<IConfiguration>()?.GetConnectionString("Default");
    }
}
