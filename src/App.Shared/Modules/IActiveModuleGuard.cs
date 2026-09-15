namespace App.Shared.Modules;

/// <summary>
/// Vérifie si un module est actif en base, pour refuser l'accès par URL (pas seulement masquer le menu).
/// </summary>
public interface IActiveModuleGuard
{
    /// <returns><see langword="true"/> si la clé est présente et marquée active.</returns>
    Task<bool> IsModuleActiveAsync(string moduleKey, CancellationToken cancellationToken = default);
}
