namespace App.Shared.Modules;

/// <summary>Persistance de l'activation on/off des modules (mono-utilisateur).</summary>
public interface IModuleActivationStore
{
    Task<ISet<string>> GetActiveKeysAsync(CancellationToken cancellationToken = default);

    Task SetActiveAsync(string moduleKey, bool isActive, CancellationToken cancellationToken = default);
}
