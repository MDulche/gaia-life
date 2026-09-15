using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace App.Core.Data;

/// <summary>
/// Factory <em>design-time</em> pour <c>dotnet ef</c> (distincte de <c>IDbContextFactory&lt;AppDbContext&gt;</c> runtime).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=localhost;Port=3306;Database=gaia_life;User=gaia;Password=changeme";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextConfiguration.Configure(optionsBuilder, connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
