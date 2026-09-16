using App.Shared.Modules;
using Microsoft.AspNetCore.Components;

namespace App.Modules.Course;

/// <summary>
/// Si le module Courses est inactif, redirige vers l'accueil (accès URL direct bloqué).
/// </summary>
internal static class CourseAccess
{
    public static async Task<bool> EnsureActiveAsync(IActiveModuleGuard guard, NavigationManager navigation)
    {
        if (await guard.IsModuleActiveAsync(CourseModule.ModuleKey))
        {
            return true;
        }

        navigation.NavigateTo("/?notice=module-disabled", replace: true);
        return false;
    }
}
