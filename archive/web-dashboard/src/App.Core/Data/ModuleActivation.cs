namespace App.Core.Data;

/// <summary>
/// Activation persistée d'un module (<see cref="ModuleKey"/> = <c>IAppModule.Key</c>).
/// Désactiver un module masque le menu et bloque les routes, sans supprimer les données métier.
/// </summary>
public class ModuleActivation
{
    public int Id { get; set; }

    /// <summary>Clé du module (unique), ex. <c>finance</c> ou <c>travail</c>.</summary>
    public string ModuleKey { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
