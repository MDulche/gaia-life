using App.Shared.Modules;

namespace App.Mobile.Modules;

/// <summary>
/// Liaisons inter-modules en Preferences locales (pas de table Admin web).
/// Par défaut : liaison active si les deux modules sont connus du catalogue.
/// </summary>
public sealed class ModuleLiaisonLocal(ModuleCatalog catalog) : IModuleLiaisonQuery
{
    private const string PrefPrefix = "liaison.active.";

    public Task<bool> IsLiaisonActiveAsync(
        string moduleA,
        string moduleB,
        CancellationToken cancellationToken = default)
    {
        if (catalog.Find(moduleA) is null || catalog.Find(moduleB) is null)
        {
            // Module non porté / non enregistré : pas d'erreur, liaison inactive.
            return Task.FromResult(false);
        }

        var key = PrefPrefix + NormalizePair(moduleA, moduleB);
        // Absent = active (comportement familial mono-utilisateur).
        return Task.FromResult(Preferences.Default.Get(key, defaultValue: true));
    }

    public Task SetLiaisonActiveAsync(string moduleA, string moduleB, bool isActive)
    {
        var key = PrefPrefix + NormalizePair(moduleA, moduleB);
        Preferences.Default.Set(key, isActive);
        return Task.CompletedTask;
    }

    private static string NormalizePair(string moduleA, string moduleB)
    {
        var a = moduleA.Trim().ToLowerInvariant();
        var b = moduleB.Trim().ToLowerInvariant();
        return string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }
}
