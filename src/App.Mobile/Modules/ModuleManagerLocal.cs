using App.Shared.Modules;

namespace App.Mobile.Modules;

/// <summary>
/// Activation locale des modules via <see cref="Preferences"/> (mono-utilisateur, pas de rôles).
/// Clé absente = module actif par défaut (premier lancement).
/// </summary>
public sealed class ModuleManagerLocal : IModuleActivationStore, IActiveModuleGuard
{
    private const string PrefPrefix = "module.active.";
    private readonly ModuleCatalog _catalog;

    public ModuleManagerLocal(ModuleCatalog catalog)
    {
        _catalog = catalog;
    }

    public Task<ISet<string>> GetActiveKeysAsync(CancellationToken cancellationToken = default)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in _catalog.AllModules)
        {
            if (IsActive(module.Key))
            {
                keys.Add(module.Key);
            }
        }

        return Task.FromResult<ISet<string>>(keys);
    }

    public Task SetActiveAsync(string moduleKey, bool isActive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);
        Preferences.Default.Set(PrefPrefix + moduleKey.ToLowerInvariant(), isActive);
        return Task.CompletedTask;
    }

    public Task<bool> IsModuleActiveAsync(string moduleKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return Task.FromResult(false);
        }

        if (_catalog.Find(moduleKey) is null)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(IsActive(moduleKey));
    }

    private static bool IsActive(string moduleKey)
    {
        var key = PrefPrefix + moduleKey.ToLowerInvariant();
        // Premier lancement : pas de préférence → module actif.
        return Preferences.Default.Get(key, defaultValue: true);
    }
}
