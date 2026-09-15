using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace App.Core.Health;

/// <summary>Espace libre sur le volume qui contient /backups (rotation 14 jours).</summary>
public sealed class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly GaiaHealthOptions _options;

    public DiskSpaceHealthCheck(IOptions<GaiaHealthOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(_options.BackupsPath);
        Directory.CreateDirectory(path);

        DriveInfo? drive = null;
        try
        {
            drive = new DriveInfo(path);
        }
        catch (ArgumentException)
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                drive = new DriveInfo(root);
            }
        }

        if (drive is null || !drive.IsReady)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Impossible de mesurer l'espace disque pour {path}."));
        }

        var free = drive.AvailableFreeSpace;
        var data = new Dictionary<string, object>
        {
            ["path"] = path,
            ["freeBytes"] = free,
            ["drive"] = drive.Name
        };

        if (free < _options.DiskUnhealthyBytes)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Espace libre {FormatSize(free)} sous le seuil critique ({FormatSize(_options.DiskUnhealthyBytes)}).",
                data: data));
        }

        if (free < _options.DiskWarningBytes)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Espace libre {FormatSize(free)} sous le seuil d'alerte ({FormatSize(_options.DiskWarningBytes)}).",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Espace libre {FormatSize(free)} sur {drive.Name}.",
            data));
    }

    public static string FormatSize(long bytes)
    {
        const double gio = 1024d * 1024d * 1024d;
        if (bytes >= gio)
        {
            return $"{bytes / gio:0.#} Gio";
        }

        const double mio = 1024d * 1024d;
        if (bytes >= mio)
        {
            return $"{bytes / mio:0.#} Mio";
        }

        const double kio = 1024d;
        if (bytes >= kio)
        {
            return $"{bytes / kio:0.#} Kio";
        }

        return $"{bytes} o";
    }
}
