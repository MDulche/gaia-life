using App.Modules.Travail.Components;
using App.Modules.Travail.Data;
using App.Modules.Travail.Migrations.Sqlite;
using App.Modules.Travail.Services;
using App.Shared.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Modules.Travail;

/// <summary>Module Travail : factory DbContext (MariaDB archive / SQLite mobile), service, widget.</summary>
public sealed class TravailModule : IAppModule
{
    public const string ModuleKey = "travail";

    public string Key => ModuleKey;

    public string DisplayName => "Travail";

    public string Icon => "bi-briefcase";

    public string RootRoute => "/travail";

    public Type? HomeWidgetComponent => typeof(TravailHomeWidget);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContextFactory<TravailDbContext>((sp, options) =>
        {
            var connectionString = ResolveConnectionString(sp);
            if (IsSqliteConnectionString(connectionString))
            {
                GaiaSqlite.Configure(
                    options,
                    connectionString,
                    TravailSqliteDesignTimeFactory.MigrationsHistoryTable,
                    typeof(TravailSqliteDbContext).Assembly.GetName().Name);
            }
            else
            {
                GaiaMariaDb.Configure(options, connectionString);
            }
        });

        services.AddDbContextFactory<TravailSqliteDbContext>((sp, options) =>
        {
            var connectionString = ResolveConnectionString(sp);
            if (!IsSqliteConnectionString(connectionString))
            {
                throw new InvalidOperationException(
                    "TravailSqliteDbContext est réservé à la chaîne SQLite (gaialife.db).");
            }

            GaiaSqlite.Configure(
                options,
                connectionString,
                TravailSqliteDesignTimeFactory.MigrationsHistoryTable,
                typeof(TravailSqliteDbContext).Assembly.GetName().Name);
        });

        services.AddScoped<TravailService>();
    }

    /// <summary>
    /// Applique les migrations : SQLite via <see cref="TravailSqliteDbContext"/>,
    /// MariaDB via <see cref="TravailDbContext"/> (archive web).
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var sqliteFactory = sp.GetService<IDbContextFactory<TravailSqliteDbContext>>();
        var connectionString = TryResolveConnectionString(sp);
        if (sqliteFactory is not null && connectionString is not null && IsSqliteConnectionString(connectionString))
        {
            await using var db = await sqliteFactory.CreateDbContextAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        var factory = sp.GetService<IDbContextFactory<TravailDbContext>>();
        if (factory is null)
        {
            return;
        }

        await using var maria = await factory.CreateDbContextAsync(cancellationToken);
        await maria.Database.MigrateAsync(cancellationToken);
    }

    private static string ResolveConnectionString(IServiceProvider sp) =>
        TryResolveConnectionString(sp)
        ?? throw new InvalidOperationException("La chaîne de connexion SQLite/MariaDB est introuvable.");

    /// <summary>Préfère <see cref="IGaiaSqliteConnection"/> (mobile), sinon ConnectionStrings:Default.</summary>
    private static string? TryResolveConnectionString(IServiceProvider sp) =>
        sp.GetService<IGaiaSqliteConnection>()?.ConnectionString
        ?? sp.GetService<IConfiguration>()?.GetConnectionString("Default");

    internal static bool IsSqliteConnectionString(string connectionString) =>
        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("Filename=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("DataSource=", StringComparison.OrdinalIgnoreCase);
}
