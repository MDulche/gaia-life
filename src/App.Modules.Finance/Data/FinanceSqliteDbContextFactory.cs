using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace App.Modules.Finance.Data;

/// <summary>
/// Factory design-time pour <c>dotnet ef</c> (migrations SQLite dans <c>Migrations/Sqlite/</c>).
/// Usage :
/// <c>dotnet ef migrations add InitialFinanceSqlite --context FinanceSqliteDbContext --output-dir Migrations/Sqlite --project src/App.Modules.Finance --startup-project src/App.Modules.Finance</c>
/// </summary>
public sealed class FinanceSqliteDbContextFactory : IDesignTimeDbContextFactory<FinanceSqliteDbContext>
{
    public FinanceSqliteDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FinanceSqliteDbContext>();
        optionsBuilder.UseSqlite("Data Source=gaialife-design.db");
        return new FinanceSqliteDbContext(optionsBuilder.Options);
    }
}
