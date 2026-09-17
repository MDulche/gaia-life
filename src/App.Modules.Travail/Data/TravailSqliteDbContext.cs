using Microsoft.EntityFrameworkCore;

namespace App.Modules.Travail.Data;

/// <summary>
/// Contexte dédié aux migrations SQLite (historique isolé
/// <c>__EFMigrationsHistory_Travail</c>). Le runtime utilise
/// <see cref="TravailDbContext"/> ; <see cref="TravailModule.MigrateAsync"/>
/// applique les migrations via ce type pour ne pas mélanger le snapshot MariaDB
/// historique (<c>Data/Migrations/</c>, archive web).
/// </summary>
public sealed class TravailSqliteDbContext : TravailDbContext
{
    public TravailSqliteDbContext(DbContextOptions<TravailSqliteDbContext> options)
        : base(options)
    {
    }
}
