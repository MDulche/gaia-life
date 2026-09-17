using App.Core.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Modules;

/// <summary>
/// Garde d'accès aux routes de module : ouvre un <see cref="AppDbContext"/> via factory
/// (le menu et la page ne doivent pas partager le même contexte).
/// </summary>
public sealed class ActiveModuleGuard : IActiveModuleGuard
{
    private readonly ModuleManager _moduleManager;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public ActiveModuleGuard(ModuleManager moduleManager, IDbContextFactory<AppDbContext> dbFactory)
    {
        _moduleManager = moduleManager;
        _dbFactory = dbFactory;
    }

    public async Task<bool> IsModuleActiveAsync(string moduleKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return false;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var keys = await _moduleManager.GetActiveModuleKeysAsync(db, cancellationToken);
        return keys.Contains(moduleKey);
    }
}
