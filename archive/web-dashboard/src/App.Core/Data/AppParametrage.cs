using App.Shared.Modules;

namespace App.Core.Data;

/// <summary>Paramétrage global de l'app (une seule ligne attendue). Défaut : mode local.</summary>
public class AppParametrage
{
    public int Id { get; set; }

    public ModeStockage Mode { get; set; } = ModeStockage.Local;

    /// <summary>Dossier hôte pour PJ / fichiers en mode local.</summary>
    public string? DossierLocal { get; set; }

    /// <summary>URL de base du stockage distant en mode serveur.</summary>
    public string? UrlServeur { get; set; }
}
