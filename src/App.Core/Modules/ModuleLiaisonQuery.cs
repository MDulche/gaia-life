using App.Core.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Modules;

/// <summary>Implémentation Core de <see cref="IModuleLiaisonQuery"/>.</summary>
public sealed class ModuleLiaisonQuery(
    IDbContextFactory<AppDbContext> dbFactory,
    LiaisonModulesService liaisons) : IModuleLiaisonQuery
{
    public async Task<bool> IsLiaisonActiveAsync(
        string moduleA,
        string moduleB,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await liaisons.IsActiveAsync(db, moduleA, moduleB, cancellationToken);
    }
}
