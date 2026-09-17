using App.Modules.Finance.Components;
using App.Modules.Finance.Data;
using App.Modules.Finance.Liaisons;
using App.Modules.Finance.Services;
using App.Shared.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Modules.Finance;

/// <summary>Module Finances : factory DbContext, service, widget d'accueil, liaison Courses.</summary>
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
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("La chaîne de connexion 'Default' est introuvable.");
            GaiaMariaDb.Configure(options, connectionString);
        });
        services.AddScoped<FinanceService>();
        services.AddScoped<IFinanceCompteCatalogue, FinanceCompteCatalogue>();
        services.AddHostedService<ArticleAcheteFinanceSubscriber>();
    }

    /// <summary>No-op si le module n'est pas enregistré (factory absente du DI).</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetService<IDbContextFactory<FinanceDbContext>>();
        if (factory is null)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
