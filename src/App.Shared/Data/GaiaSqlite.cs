using Microsoft.EntityFrameworkCore;

namespace App.Shared.Data;

/// <summary>Configuration EF Core SQLite commune (fichier unique gaialife.db).</summary>
public static class GaiaSqlite
{
    public const string DefaultFileName = "gaialife.db";

    /// <param name="appDataDirectory">Répertoire applicatif (<see cref="FileSystem.AppDataDirectory"/>).</param>
    public static string BuildConnectionString(string appDataDirectory) =>
        $"Data Source={Path.Combine(appDataDirectory, DefaultFileName)}";

    /// <summary>
    /// Configure SQLite + table d'historique de migrations isolée par module
    /// (plusieurs DbContext sur le même fichier).
    /// </summary>
    public static void Configure(
        DbContextOptionsBuilder options,
        string connectionString,
        string migrationsHistoryTable,
        string? migrationsAssembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsHistoryTable);

        options.UseSqlite(connectionString, sqlite =>
        {
            sqlite.MigrationsHistoryTable(migrationsHistoryTable);
            if (!string.IsNullOrWhiteSpace(migrationsAssembly))
            {
                sqlite.MigrationsAssembly(migrationsAssembly);
            }
        });
    }
}
