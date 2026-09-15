using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace App.Core.Health;

/// <summary>Unhealthy si aucun dump quotidien n'a moins de <see cref="GaiaHealthOptions.BackupMaxAgeHours"/> heures.</summary>
public sealed class LastBackupHealthCheck : IHealthCheck
{
    private readonly BackupFolderMonitor _monitor;
    private readonly GaiaHealthOptions _options;

    public LastBackupHealthCheck(BackupFolderMonitor monitor, IOptions<GaiaHealthOptions> options)
    {
        _monitor = monitor;
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = _monitor.GetSnapshot();
        var maxAge = TimeSpan.FromHours(_options.BackupMaxAgeHours);
        var data = new Dictionary<string, object>
        {
            ["path"] = snapshot.Path,
            ["fileCount"] = snapshot.FileCount,
            ["maxAgeHours"] = _options.BackupMaxAgeHours
        };

        if (!snapshot.Exists)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Le dossier de sauvegarde {snapshot.Path} est introuvable.",
                data: data));
        }

        if (snapshot.LastBackupUtc is null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Aucune sauvegarde quotidien_*.sql.gz dans le dossier.",
                data: data));
        }

        data["lastBackupUtc"] = snapshot.LastBackupUtc.Value;
        data["lastBackupFile"] = snapshot.LastBackupFileName ?? string.Empty;

        var age = DateTimeOffset.UtcNow - snapshot.LastBackupUtc.Value;
        if (age > maxAge)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Dernière sauvegarde il y a {FormatAge(age)} ({snapshot.LastBackupFileName}), seuil {_options.BackupMaxAgeHours:0.#} h.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Dernière sauvegarde {snapshot.LastBackupFileName} ({FormatAge(age)}).",
            data));
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalHours >= 1)
        {
            return $"{age.TotalHours:0.#} h";
        }

        return $"{age.TotalMinutes:0} min";
    }
}
