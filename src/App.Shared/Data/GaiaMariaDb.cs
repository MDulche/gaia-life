using Microsoft.EntityFrameworkCore;

namespace App.Shared.Data;

/// <summary>
/// Configuration Pomelo unique pour tous les DbContext (Core, Finance, Travail).
/// La version MariaDB est figée : <c>ServerVersion.AutoDetect</c> ouvrirait une connexion à chaque création
/// de contexte et contournerait la politique de retry.
/// </summary>
public static class GaiaMariaDb
{
    /// <summary>MariaDB 11.6, alignée sur l'image Docker du compose.</summary>
    public static readonly ServerVersion ServerVersion = new MariaDbServerVersion(new Version(11, 6, 0));

    /// <summary>
    /// Active Pomelo avec retry (coupure / redémarrage du conteneur MariaDB).
    /// Codes extra : 0, 1042, 2002, 2003, 2006, 2013 (perte de connexion typique).
    /// </summary>
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
