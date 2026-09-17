using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace App.Modules.Stock.Data;

/// <summary>
/// N'expose que les migrations du namespace <c>App.Modules.Stock.Migrations.Sqlite</c>
/// (historique MariaDB archivé hors dépôt actif).
/// </summary>
#pragma warning disable EF1001 // MigrationsAssembly is an internal EF API we intentionally replace
public sealed class StockSqliteMigrationsAssembly : MigrationsAssembly
{
    private const string SqliteNamespace = "App.Modules.Stock.Migrations.Sqlite";

    public StockSqliteMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        : base(currentContext, options, idGenerator, logger)
    {
    }

    public override IReadOnlyDictionary<string, TypeInfo> Migrations =>
        base.Migrations
            .Where(pair => pair.Value.Namespace?.StartsWith(SqliteNamespace, StringComparison.Ordinal) == true)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
}
#pragma warning restore EF1001

public static class StockSqliteEfExtensions
{
    public static DbContextOptionsBuilder UseStockSqlite(
        this DbContextOptionsBuilder options,
        string connectionString)
    {
        GaiaSqliteConfigure(options, connectionString);
        options.ReplaceService<IMigrationsAssembly, StockSqliteMigrationsAssembly>();
        return options;
    }

    private static void GaiaSqliteConfigure(DbContextOptionsBuilder options, string connectionString)
    {
        App.Shared.Data.GaiaSqlite.Configure(
            options,
            connectionString,
            migrationsHistoryTable: "__EFMigrationsHistory_Stock",
            migrationsAssembly: typeof(StockDbContext).Assembly.GetName().Name);
    }
}
