namespace App.Shared.Modules;

public interface IActiveModuleGuard
{
    Task<bool> IsModuleActiveAsync(string moduleKey, CancellationToken cancellationToken = default);
}
