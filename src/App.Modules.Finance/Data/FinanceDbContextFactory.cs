using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace App.Modules.Finance.Data;

/// <summary>Factory design-time pour <c>dotnet ef</c> (migrations Finance).</summary>
public sealed class FinanceDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile(Path.Combine("src", "App.Core", "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine("src", "App.Core", "appsettings.Development.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=localhost;Port=3306;Database=gaia_life;User=gaia;Password=changeme";

        var optionsBuilder = new DbContextOptionsBuilder<FinanceDbContext>();
        GaiaMariaDb.Configure(optionsBuilder, connectionString);
        return new FinanceDbContext(optionsBuilder.Options);
    }
}
