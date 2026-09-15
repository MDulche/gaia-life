using App.Shared.Modules;
using Microsoft.AspNetCore.Components;

namespace App.Modules.Travail;

/// <summary>Bloque l'accès URL si le module Travail est inactif (même logique que Finance).</summary>
internal static class TravailAccess
{
    public static async Task<bool> EnsureActiveAsync(IActiveModuleGuard guard, NavigationManager navigation)
    {
        if (await guard.IsModuleActiveAsync(TravailModule.ModuleKey))
        {
            return true;
        }

        navigation.NavigateTo("/?notice=module-disabled", replace: true);
        return false;
    }
}
