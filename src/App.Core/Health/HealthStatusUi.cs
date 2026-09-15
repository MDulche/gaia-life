using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace App.Core.Health;

/// <summary>Libellés, icônes et classes CSS des statuts de santé (couleur + pictogramme pour le daltonisme).</summary>
public static class HealthStatusUi
{
    public static string DisplayName(string checkName) => checkName switch
    {
        "mariadb" => "Base de données",
        "espace-disque" => "Espace disque",
        "derniere-sauvegarde" => "Dernière sauvegarde",
        _ => checkName
    };

    public static string Label(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "OK",
        HealthStatus.Degraded => "Alerte",
        _ => "KO"
    };

    public static string Icon(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "✓",
        HealthStatus.Degraded => "!",
        _ => "×"
    };

    public static string BadgeClass(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "health-badge health-badge-ok",
        HealthStatus.Degraded => "health-badge health-badge-warn",
        _ => "health-badge health-badge-ko"
    };

    public static string AlertClass(HealthStatus status) => status switch
    {
        HealthStatus.Degraded => "alert-warning",
        HealthStatus.Unhealthy => "alert-danger",
        _ => "alert-success"
    };

    public static string Detail(HealthReportEntry entry)
    {
        var raw = entry.Description ?? entry.Exception?.Message;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return entry.Status == HealthStatus.Healthy ? "—" : Label(entry.Status);
        }

        if (raw.Contains("canceled", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return "Le service ne répond pas (délai dépassé).";
        }

        return raw;
    }
}
