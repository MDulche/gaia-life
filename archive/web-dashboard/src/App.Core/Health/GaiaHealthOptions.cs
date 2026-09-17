namespace App.Core.Health;

/// <summary>
/// Seuils et chemins des health checks (disque /backups, âge de la dernière sauvegarde).
/// </summary>
public sealed class GaiaHealthOptions
{
    public const string SectionName = "HealthChecks";

    /// <summary>Dossier des dumps <c>quotidien_*.sql.gz</c> (bind mount Docker <c>/backups</c>).</summary>
    public string BackupsPath { get; set; } = "backups";

    /// <summary>Dossier des fichiers Serilog <c>gaia-YYYYMMDD.log</c>.</summary>
    public string LogsPath { get; set; } = "logs";

    /// <summary>En dessous : Degraded (orange). Défaut 5 Gio.</summary>
    public long DiskWarningBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>En dessous : Unhealthy (rouge). Défaut 1 Gio.</summary>
    public long DiskUnhealthyBytes { get; set; } = 1L * 1024 * 1024 * 1024;

    /// <summary>Unhealthy si le dump le plus récent est plus vieux. Défaut 26 h (cron 3h + marge).</summary>
    public double BackupMaxAgeHours { get; set; } = 26;

    /// <summary>Période du publisher (détection MariaDB sans attendre une requête /health).</summary>
    public int PublishPeriodSeconds { get; set; } = 15;
}
