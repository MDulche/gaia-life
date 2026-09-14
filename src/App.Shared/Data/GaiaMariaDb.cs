using Microsoft.EntityFrameworkCore;

namespace App.Shared.Data;

public static class GaiaMariaDb
{
    public static readonly ServerVersion ServerVersion = new MariaDbServerVersion(new Version(11, 6, 0));

    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseMySql(
            connectionString,
            ServerVersion,
            mysql => mysql.EnableRetryOnFailure(
                maxRetryCount: 15,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: [0, 1042, 2002, 2003, 2006, 2013]));
    }
}
