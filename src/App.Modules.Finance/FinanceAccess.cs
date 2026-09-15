using App.Shared.Modules;
using Microsoft.AspNetCore.Components;

namespace App.Modules.Finance;

/// <summary>
/// Si le module Finances est inactif, redirige vers l'accueil (accès URL direct bloqué, pas seulement le menu).
/// </summary>
internal static class FinanceAccess
{
    public static async Task<bool> EnsureActiveAsync(IActiveModuleGuard guard, NavigationManager navigation)
    {
        if (await guard.IsModuleActiveAsync(FinanceModule.ModuleKey))
        {
            return true;
        }

        navigation.NavigateTo("/?notice=module-disabled", replace: true);
        return false;
    }
}
