using App.Modules.Course.Components;
using App.Modules.Course.Data;
using App.Modules.Course.Entities;
using App.Modules.Course.Services;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.Modules.Course;

/// <summary>Module Courses : SQLite partagé (mobile), service, widget d'accueil.</summary>
public sealed class CourseModule : IAppModule
{
    public const string ModuleKey = "course";

    public string Key => ModuleKey;

    public string DisplayName => "Courses";

    public string Icon => "bi-cart";

    public string RootRoute => "/course";

    public Type? HomeWidgetComponent => typeof(CourseHomeWidget);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContextFactory<CourseDbContext>((sp, options) =>
        {
            // Même chaîne SQLite que les autres modules (ConnectionStrings:Default → gaialife.db).
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("La chaîne de connexion 'Default' (SQLite) est introuvable.");
            options.UseSqlite(connectionString);
        });
        services.AddScoped<CourseService>();
        services.AddScoped<ICourseParametresQuery>(sp => sp.GetRequiredService<CourseService>());

        // Stubs si Finance/Stock pas encore branchés — TryAdd pour laisser un vrai catalogue les remplacer.
        services.TryAddScoped<IModuleLiaisonQuery, NullModuleLiaisonQuery>();
        services.TryAddScoped<IStockArticleCatalogue, NullStockArticleCatalogue>();
        services.TryAddScoped<IFinanceCompteCatalogue, NullFinanceCompteCatalogue>();
    }

    /// <summary>No-op si le module n'est pas enregistré (factory absente du DI).</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetService<IDbContextFactory<CourseDbContext>>();
        if (factory is null)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        // Premier lancement mobile : catégorie minimale pour pouvoir ajouter des articles
        // (pas d'écran admin courses porté pour l'instant).
        if (!await db.Categories.AnyAsync(cancellationToken))
        {
            db.Categories.Add(new Categorie { Nom = "Divers", Couleur = "#6c757d" });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
