using App.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Data;

internal static class AppDbContextConfiguration
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        GaiaMariaDb.Configure(options, connectionString);
}
