using App.Shared.Modules;
using Microsoft.AspNetCore.Components;

namespace App.Modules.Finance;

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
