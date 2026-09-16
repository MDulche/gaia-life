namespace App.Shared.Modules;

/// <summary>Lit le paramétrage global (mode local / serveur) pour les modules.</summary>
public interface IAppParametrageQuery
{
    Task<ModeStockage> GetModeAsync(CancellationToken cancellationToken = default);

    /// <summary>Chemin local ou URL serveur selon le mode (peut être vide).</summary>
    Task<string?> GetEmplacementStockageAsync(CancellationToken cancellationToken = default);

    /// <summary>Les intégrations IA ne sont disponibles qu'en mode serveur.</summary>
    Task<bool> AreIntegrationsIaDisponiblesAsync(CancellationToken cancellationToken = default);
}
