using App.Core.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Modules;

public sealed class ModuleManager
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

    public async Task<ISet<string>> GetActiveModuleKeysAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var keys = await db.ModuleActivations
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => m.ModuleKey)
            .ToListAsync(cancellationToken);

        return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetModuleActiveAsync(
        AppDbContext db,
        string key,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var entity = await db.ModuleActivations
            .FirstOrDefaultAsync(m => m.ModuleKey == key, cancellationToken);

        if (entity is null)
        {
            db.ModuleActivations.Add(new ModuleActivation
            {
                ModuleKey = key,
                IsActive = isActive
            });
        }
        else
        {
            entity.IsActive = isActive;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
