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

/// <summary>Module Travail : factory DbContext SQLite, service, widget.</summary>
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
            ConfigureSqlite(options, ResolveConnectionString(sp));
        });

        services.AddDbContextFactory<TravailSqliteDbContext>((sp, options) =>
        {
            ConfigureSqlite(options, ResolveConnectionString(sp));
        });

        services.AddScoped<TravailService>();
    }

    /// <summary>Applique les migrations SQLite via <see cref="TravailSqliteDbContext"/>.</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var sqliteFactory = sp.GetService<IDbContextFactory<TravailSqliteDbContext>>();
        if (sqliteFactory is null)
        {
            return;
        }

        await using var db = await sqliteFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static void ConfigureSqlite(DbContextOptionsBuilder options, string connectionString) =>
        GaiaSqlite.Configure(
            options,
            connectionString,
            TravailSqliteDesignTimeFactory.MigrationsHistoryTable,
            typeof(TravailSqliteDbContext).Assembly.GetName().Name);

    private static string ResolveConnectionString(IServiceProvider sp) =>
        TryResolveConnectionString(sp)
        ?? throw new InvalidOperationException("La chaîne de connexion SQLite est introuvable.");

    /// <summary>Préfère <see cref="IGaiaSqliteConnection"/> (mobile), sinon ConnectionStrings:Default.</summary>
    private static string? TryResolveConnectionString(IServiceProvider sp) =>
        sp.GetService<IGaiaSqliteConnection>()?.ConnectionString
        ?? sp.GetService<IConfiguration>()?.GetConnectionString("Default");
}
