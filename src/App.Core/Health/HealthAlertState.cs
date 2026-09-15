using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace App.Core.Health;

/// <summary>
/// Dernier rapport de santé, partagé entre le menu, l'accueil et /admin.
/// Mis à jour par le publisher (toutes les 15 s) et par le bouton Rafraîchir.
/// </summary>
public sealed class HealthAlertState
{
    public HealthStatus Status { get; private set; } = HealthStatus.Healthy;

    public string Summary { get; private set; } = string.Empty;

    public bool HasAlert => Status is HealthStatus.Degraded or HealthStatus.Unhealthy;

    public event Action? Changed;

    public void Apply(HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        Status = report.Status;
        var failing = report.Entries
            .Where(entry => entry.Value.Status != HealthStatus.Healthy)
            .Select(entry => (
                Name: HealthStatusUi.DisplayName(entry.Key),
                Status: entry.Value.Status,
                Detail: HealthStatusUi.Detail(entry.Value)))
            .ToList();

        if (failing.Count == 0)
        {
            Summary = string.Empty;
        }
        else if (failing.Count == 1)
        {
            var item = failing[0];
            Summary = $"{item.Name} ({HealthStatusUi.Label(item.Status)}) : {item.Detail}";
        }
        else
        {
            Summary = $"{failing.Count} contrôles en alerte, dont {failing[0].Name} ({HealthStatusUi.Label(failing[0].Status)}).";
        }

        Changed?.Invoke();
    }
}
