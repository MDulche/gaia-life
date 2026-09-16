namespace App.Shared.Modules;

/// <summary>
/// Interroge l'état des liaisons entre modules (config Admin), sans sync métier.
/// </summary>
public interface IModuleLiaisonQuery
{
    /// <returns><see langword="true"/> si la paire est marquée active en base.</returns>
    Task<bool> IsLiaisonActiveAsync(string moduleA, string moduleB, CancellationToken cancellationToken = default);
}
