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

/// <summary>Module Finances : factory DbContext (MariaDB archive / SQLite mobile), service, liaison Courses.</summary>
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
            var connectionString = ResolveConnectionString(sp);
            if (IsSqliteConnectionString(connectionString))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                GaiaMariaDb.Configure(options, connectionString);
            }
        });

        // Factory compagnon pour MigrateAsync SQLite (migrations dans Migrations/Sqlite/).
        services.AddDbContextFactory<FinanceSqliteDbContext>((sp, options) =>
        {
            var connectionString = ResolveConnectionString(sp);
            if (!IsSqliteConnectionString(connectionString))
            {
                throw new InvalidOperationException(
                    "FinanceSqliteDbContext est réservé à la chaîne SQLite (gaialife.db).");
            }

            options.UseSqlite(connectionString);
        });

        services.AddScoped<FinanceService>();
        services.AddScoped<IFinanceCompteCatalogue, FinanceCompteCatalogue>();
        services.TryAddSingleton<IModuleLiaisonQuery, NullModuleLiaisonQuery>();
        services.TryAddSingleton<ICourseParametresQuery, NullCourseParametresQuery>();
        services.AddSingleton<ArticleAcheteFinanceSubscriber>();
        services.AddHostedService(sp => sp.GetRequiredService<ArticleAcheteFinanceSubscriber>());
    }

    /// <summary>
    /// Applique les migrations : SQLite via <see cref="FinanceSqliteDbContext"/>,
    /// MariaDB via <see cref="FinanceDbContext"/> (archive web).
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var sqliteFactory = sp.GetService<IDbContextFactory<FinanceSqliteDbContext>>();
        var connectionString = TryResolveConnectionString(sp);
        if (sqliteFactory is not null && connectionString is not null && IsSqliteConnectionString(connectionString))
        {
            await using var db = await sqliteFactory.CreateDbContextAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        var factory = sp.GetService<IDbContextFactory<FinanceDbContext>>();
        if (factory is null)
        {
            return;
        }

        await using var maria = await factory.CreateDbContextAsync(cancellationToken);
        await maria.Database.MigrateAsync(cancellationToken);
    }

    private static string ResolveConnectionString(IServiceProvider sp) =>
        TryResolveConnectionString(sp)
        ?? throw new InvalidOperationException("La chaîne de connexion 'Default' est introuvable.");

    private static string? TryResolveConnectionString(IServiceProvider sp)
    {
        var sqlite = sp.GetService<GaiaSqliteOptions>();
        if (sqlite is not null && !string.IsNullOrWhiteSpace(sqlite.ConnectionString))
        {
            return sqlite.ConnectionString;
        }

        return sp.GetService<IConfiguration>()?.GetConnectionString("Default");
    }

    internal static bool IsSqliteConnectionString(string connectionString) =>
        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("Filename=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("DataSource=", StringComparison.OrdinalIgnoreCase);
}
