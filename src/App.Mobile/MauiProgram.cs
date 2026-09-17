using App.Mobile.Modules;
using App.Shared.Events;
using App.Shared.Modules;
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

        var catalog = new ModuleCatalog();
        // Les agents de module enregistreront FinanceModule / TravailModule / CourseModule / StockModule ici.
        builder.Services.AddSingleton(catalog);
        builder.Services.AddSingleton<ModuleManagerLocal>();
        builder.Services.AddSingleton<IModuleActivationStore>(sp => sp.GetRequiredService<ModuleManagerLocal>());
        builder.Services.AddSingleton<IActiveModuleGuard>(sp => sp.GetRequiredService<ModuleManagerLocal>());
        builder.Services.AddSingleton<IEvenementBus, EvenementBus>();

        // ConfigureServices des modules (quand enregistrés) — pour l’instant catalogue vide.
        catalog.ConfigureAllServices(builder.Services);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
