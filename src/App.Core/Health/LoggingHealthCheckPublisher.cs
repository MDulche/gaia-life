using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace App.Core.Health;

/// <summary>
/// Exécute les health checks en arrière-plan (période configurable) et journalise les états non Healthy.
/// MariaDB down est donc visible sans attendre un hit /health ou /admin.
/// </summary>
public sealed class LoggingHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<LoggingHealthCheckPublisher> _logger;
    private readonly GaiaHealthOptions _options;
    private readonly HealthAlertState _alerts;

    public LoggingHealthCheckPublisher(
        ILogger<LoggingHealthCheckPublisher> logger,
        IOptions<GaiaHealthOptions> options,
        HealthAlertState alerts)
    {
        _logger = logger;
        _options = options.Value;
        _alerts = alerts;
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        _alerts.Apply(report);

        foreach (var (name, entry) in report.Entries)
        {
            if (entry.Status == HealthStatus.Healthy)
            {
                continue;
            }

            _logger.LogWarning(
                "Health check {HealthCheckName} {HealthStatus}: {HealthDescription}",
                name,
                entry.Status,
                entry.Description ?? entry.Exception?.Message ?? string.Empty);
        }

        if (report.Status == HealthStatus.Unhealthy)
        {
            _logger.LogError(
                "Rapport de santé global {HealthStatus} ({CheckCount} checks, période {PeriodSeconds}s)",
                report.Status,
                report.Entries.Count,
                _options.PublishPeriodSeconds);
        }

        return Task.CompletedTask;
    }
}
