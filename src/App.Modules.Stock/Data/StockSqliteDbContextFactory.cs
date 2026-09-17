using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace App.Modules.Stock.Data;

/// <summary>
/// Factory design-time pour les migrations SQLite (<c>Migrations/Sqlite/</c>).
/// </summary>
public sealed class StockSqliteDbContextFactory : IDesignTimeDbContextFactory<StockDbContext>
{
    public StockDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StockDbContext>();
        var designDir = Path.Combine(Path.GetTempPath(), "gaia-stock-design");
        Directory.CreateDirectory(designDir);
        optionsBuilder.UseStockSqlite(GaiaSqlite.BuildConnectionString(designDir));
        return new StockDbContext(optionsBuilder.Options);
    }
}
