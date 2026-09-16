using App.Shared.Modules;
using Microsoft.AspNetCore.Components;

namespace App.Modules.Stock;

/// <summary>
/// Si le module Stock est inactif, redirige vers l'accueil (accès URL direct bloqué).
/// </summary>
internal static class StockAccess
{
    public static async Task<bool> EnsureActiveAsync(IActiveModuleGuard guard, NavigationManager navigation)
    {
        if (await guard.IsModuleActiveAsync(StockModule.ModuleKey))
        {
            return true;
        }

        navigation.NavigateTo("/?notice=module-disabled", replace: true);
        return false;
    }
}
