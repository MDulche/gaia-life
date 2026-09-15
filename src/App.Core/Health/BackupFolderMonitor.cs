using Microsoft.Extensions.Options;

namespace App.Core.Health;

/// <summary>État du dossier des dumps, pour /admin et le health check « dernière sauvegarde ».</summary>
public sealed class BackupFolderSnapshot
{
    public required string Path { get; init; }

    public bool Exists { get; init; }

    public int FileCount { get; init; }

    public long TotalBytes { get; init; }

    public DateTimeOffset? LastBackupUtc { get; init; }

    public string? LastBackupFileName { get; init; }
}

/// <summary>Lit le volume /backups sans ouvrir de connexion SQL.</summary>
public sealed class BackupFolderMonitor
{
    private readonly GaiaHealthOptions _options;

    public BackupFolderMonitor(IOptions<GaiaHealthOptions> options)
    {
        _options = options.Value;
    }

    public string BackupsPath => _options.BackupsPath;

    public BackupFolderSnapshot GetSnapshot()
    {
        var path = _options.BackupsPath;
        if (!Directory.Exists(path))
        {
            return new BackupFolderSnapshot
            {
                Path = path,
                Exists = false,
                FileCount = 0,
                TotalBytes = 0
            };
        }

        var files = new DirectoryInfo(path)
            .EnumerateFiles("quotidien_*.sql.gz", SearchOption.TopDirectoryOnly)
            .ToList();

        FileInfo? newest = files.Count == 0
            ? null
            : files.MaxBy(f => f.LastWriteTimeUtc);

        return new BackupFolderSnapshot
        {
            Path = path,
            Exists = true,
            FileCount = files.Count,
            TotalBytes = files.Sum(f => f.Length),
            LastBackupUtc = newest is null ? null : new DateTimeOffset(newest.LastWriteTimeUtc),
            LastBackupFileName = newest?.Name
        };
    }
}
