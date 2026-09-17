using App.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Data;

/// <summary>Délègue la config Pomelo/retry à <see cref="GaiaMariaDb"/>.</summary>
internal static class AppDbContextConfiguration
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        GaiaMariaDb.Configure(options, connectionString);
}
