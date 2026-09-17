using App.Mobile.Modules;
using App.Modules.Course;
using App.Modules.Finance;
using App.Modules.Stock;
using App.Modules.Travail;
using App.Shared.Events;
using App.Shared.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<GaiaLifeApp>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Un seul fichier SQLite partagé (contrat migration mobile).
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "gaialife.db");
        var sqliteCs = $"Data Source={dbPath}";
        builder.Services.AddSingleton<IConfiguration>(new SqliteConnectionConfiguration(sqliteCs));

        var catalog = new ModuleCatalog();
        catalog.Register(new FinanceModule());
        catalog.Register(new TravailModule());
        catalog.Register(new CourseModule());
        catalog.Register(new StockModule());
        builder.Services.AddSingleton(catalog);
        builder.Services.AddSingleton<ModuleManagerLocal>();
        builder.Services.AddSingleton<IModuleActivationStore>(sp => sp.GetRequiredService<ModuleManagerLocal>());
        builder.Services.AddSingleton<IActiveModuleGuard>(sp => sp.GetRequiredService<ModuleManagerLocal>());
        // Preferences locales — remplace NullModuleLiaisonQuery (TryAdd Course).
        builder.Services.AddSingleton<IModuleLiaisonQuery, ModuleLiaisonLocal>();
        builder.Services.AddSingleton<IEvenementBus, EvenementBus>();

        catalog.ConfigureAllServices(builder.Services);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Migrations + abonnements hosted (MAUI ne démarre pas toujours les IHostedService tout seul).
        BootstrapModulesAsync(app.Services).GetAwaiter().GetResult();

        return app;
    }

    private static async Task BootstrapModulesAsync(IServiceProvider services)
    {
        await FinanceModule.MigrateAsync(services).ConfigureAwait(false);
        await TravailModule.MigrateAsync(services).ConfigureAwait(false);
        await CourseModule.MigrateAsync(services).ConfigureAwait(false);
        await StockModule.MigrateAsync(services).ConfigureAwait(false);

        foreach (var hosted in services.GetServices<IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
