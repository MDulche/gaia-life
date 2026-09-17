using Microsoft.EntityFrameworkCore;

namespace App.Modules.Finance.Data;

/// <summary>
/// Contexte compagnon pour les migrations SQLite uniquement (même modèle que <see cref="FinanceDbContext"/>).
/// Évite le conflit avec les migrations MariaDB historiques dans <c>Data/Migrations/</c>.
/// </summary>
public sealed class FinanceSqliteDbContext : FinanceDbContext
{
    public FinanceSqliteDbContext(DbContextOptions<FinanceSqliteDbContext> options)
        : base(options)
    {
    }
}
