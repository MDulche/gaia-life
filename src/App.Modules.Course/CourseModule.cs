using App.Modules.Course.Components;
using App.Modules.Course.Data;
using App.Modules.Course.Services;
using App.Shared.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Modules.Course;

/// <summary>Module Courses : factory DbContext, service, widget d'accueil.</summary>
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
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("La chaîne de connexion 'Default' est introuvable.");
            GaiaMariaDb.Configure(options, connectionString);
        });
        services.AddScoped<CourseService>();
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
    }
}
