using App.Modules.Travail.Components;
using App.Modules.Travail.Data;
using App.Modules.Travail.Services;
using App.Shared.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Modules.Travail;

/// <summary>Module Travail : factory DbContext, service, widget d'accueil.</summary>
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
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("La chaîne de connexion 'Default' est introuvable.");
            GaiaMariaDb.Configure(options, connectionString);
        });
        services.AddScoped<TravailService>();
    }

    /// <summary>No-op si le module n'est pas enregistré (factory absente du DI).</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetService<IDbContextFactory<TravailDbContext>>();
        if (factory is null)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
