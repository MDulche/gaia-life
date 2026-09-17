using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace App.Modules.Stock.Data;

/// <summary>
/// Helper MariaDB pour l'archive web uniquement.
/// Design-time mobile = <see cref="StockSqliteDbContextFactory"/> (évite deux IDesignTimeDbContextFactory).
/// </summary>
public static class StockMariaDbDesignHelper
{
    public static StockDbContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=localhost;Port=3306;Database=gaia_life;User=gaia;Password=changeme";

        var optionsBuilder = new DbContextOptionsBuilder<StockDbContext>();
        GaiaMariaDb.Configure(optionsBuilder, connectionString);
        return new StockDbContext(optionsBuilder.Options);
    }
}
