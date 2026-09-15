using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace App.Core.Health;

public sealed class LogLine
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Level { get; init; }

    public required string Message { get; init; }
}

/// <summary>Lit les 20 derniers Warning/Error du fichier Serilog du jour (partage le fichier avec le sink).</summary>
public sealed class SerilogFileTail
{
    private static readonly Regex LinePattern = new(
        @"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?(?: [+-]\d{2}:\d{2})?) \[(?<lvl>WRN|ERR|FTL)\] (?<msg>.*)$",
        RegexOptions.Compiled);

    private readonly GaiaHealthOptions _options;

    public SerilogFileTail(IOptions<GaiaHealthOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<LogLine> ReadRecentWarnings(int count = 20)
    {
        var dir = _options.LogsPath;
        if (!Directory.Exists(dir))
        {
            return [];
        }

        var today = Path.Combine(dir, $"gaia-{DateTime.Now:yyyyMMdd}.log");
        var files = new List<string>();
        if (File.Exists(today))
        {
            files.Add(today);
        }

        var yesterday = Path.Combine(dir, $"gaia-{DateTime.Now.AddDays(-1):yyyyMMdd}.log");
        if (File.Exists(yesterday))
        {
            files.Add(yesterday);
        }

        var lines = new List<LogLine>();
        foreach (var file in files.OrderBy(f => f))
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                string? raw;
                while ((raw = reader.ReadLine()) is not null)
                {
                    var match = LinePattern.Match(raw);
                    if (!match.Success)
                    {
                        continue;
                    }

                    if (!DateTimeOffset.TryParse(match.Groups["ts"].Value, out var ts))
                    {
                        continue;
                    }

                    lines.Add(new LogLine
                    {
                        Timestamp = ts,
                        Level = match.Groups["lvl"].Value,
                        Message = match.Groups["msg"].Value.Trim()
                    });
                }
            }
            catch (IOException)
            {
                // Fichier en cours d'écriture : on affiche ce qui a déjà été lu.
            }
        }

        return lines
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToList();
    }
}
