using Microsoft.Extensions.DependencyInjection;

namespace App.Shared.Modules;

/// <summary>
/// Catalogue des modules compilés (enregistrement + ConfigureServices).
/// L'activation on/off est déléguée à un store local (mobile) ou web archivé.
/// </summary>
public sealed class ModuleCatalog
{
    private readonly List<IAppModule> _modules = [];

    public IReadOnlyList<IAppModule> AllModules => _modules;

    public void Register(IAppModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        if (_modules.Any(m => string.Equals(m.Key, module.Key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Le module '{module.Key}' est déjà enregistré.");
        }

        _modules.Add(module);
    }

    public IAppModule? Find(string key) =>
        _modules.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<IAppModule> GetActiveModules(ISet<string> activeKeys)
    {
        ArgumentNullException.ThrowIfNull(activeKeys);
        return _modules.Where(module => activeKeys.Contains(module.Key)).ToList();
    }

    public void ConfigureAllServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var module in _modules)
        {
            module.ConfigureServices(services);
        }
    }
}
