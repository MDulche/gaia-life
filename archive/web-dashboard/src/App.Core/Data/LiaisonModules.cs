namespace App.Core.Data;

/// <summary>
/// Liaison configurée entre deux modules (état on/off uniquement).
/// Aucune synchronisation métier n'est déclenchée à ce stade.
/// </summary>
public class LiaisonModules
{
    public int Id { get; set; }

    /// <summary>Clé du premier module (ex. <c>course</c>).</summary>
    public string ModuleA { get; set; } = string.Empty;

    /// <summary>Clé du second module (ex. <c>finance</c>).</summary>
    public string ModuleB { get; set; } = string.Empty;

    public bool EstActive { get; set; }
}
