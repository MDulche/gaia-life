using Microsoft.EntityFrameworkCore;

namespace App.Core.Data;

/// <summary>Délègue la config Pomelo/retry à <see cref="GaiaMariaDb"/> (copie locale archive).</summary>
internal static class AppDbContextConfiguration
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        GaiaMariaDb.Configure(options, connectionString);
}
