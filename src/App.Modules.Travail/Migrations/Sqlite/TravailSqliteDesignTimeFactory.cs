using App.Modules.Travail.Data;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace App.Modules.Travail.Migrations.Sqlite;

/// <summary>
/// Factory design-time SQLite pour <c>dotnet ef migrations add … --context TravailSqliteDbContext</c>.
/// Ne remplace pas <see cref="TravailDbContextFactory"/> (MariaDB / archive).
/// </summary>
public sealed class TravailSqliteDesignTimeFactory : IDesignTimeDbContextFactory<TravailSqliteDbContext>
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Travail";

    public TravailSqliteDbContext CreateDbContext(string[] args)
    {
        // BuildConnectionString attend un répertoire AppData, pas un chemin de fichier.
        var connectionString = GaiaSqlite.BuildConnectionString(Path.GetTempPath());
        var optionsBuilder = new DbContextOptionsBuilder<TravailSqliteDbContext>();
        GaiaSqlite.Configure(
            optionsBuilder,
            connectionString,
            MigrationsHistoryTable,
            typeof(TravailSqliteDbContext).Assembly.GetName().Name);
        return new TravailSqliteDbContext(optionsBuilder.Options);
    }
}
