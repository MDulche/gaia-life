using App.Modules.Finance.Data;
using App.Modules.Finance.Entities;
using App.Modules.Finance.Liaisons;
using App.Modules.Finance.Services;
using App.Shared.Events;
using App.Shared.Modules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Modules.Finance.Tests;

internal sealed class FinanceTestFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public FinanceService Finance { get; }

    public IEvenementBus Bus { get; }

    public IDbContextFactory<FinanceDbContext> Factory { get; }

    public TestModuleGuard Guard { get; }

    public TestLiaisonQuery Liaisons { get; }

    public TestCourseParametres CourseParams { get; }

    private FinanceTestFixture(
        SqliteConnection connection,
        ServiceProvider provider,
        FinanceService finance,
        IEvenementBus bus,
        IDbContextFactory<FinanceDbContext> factory,
        TestModuleGuard guard,
        TestLiaisonQuery liaisons,
        TestCourseParametres courseParams)
    {
        _connection = connection;
        _provider = provider;
        Finance = finance;
        Bus = bus;
        Factory = factory;
        Guard = guard;
        Liaisons = liaisons;
        CourseParams = courseParams;
    }

    public static async Task<FinanceTestFixture> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddDbContextFactory<FinanceDbContext>(o => o.UseSqlite(connection));
        services.AddDbContextFactory<FinanceSqliteDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<FinanceService>();
        services.AddLogging();
        services.AddSingleton<IEvenementBus>(_ => new EvenementBus(NullLogger<EvenementBus>.Instance));
        services.AddSingleton<TestModuleGuard>();
        services.AddSingleton<IActiveModuleGuard>(sp => sp.GetRequiredService<TestModuleGuard>());
        services.AddSingleton<TestLiaisonQuery>();
        services.AddSingleton<IModuleLiaisonQuery>(sp => sp.GetRequiredService<TestLiaisonQuery>());
        services.AddSingleton<TestCourseParametres>();
        services.AddSingleton<ICourseParametresQuery>(sp => sp.GetRequiredService<TestCourseParametres>());
        services.AddSingleton<ArticleAcheteFinanceSubscriber>();

        var provider = services.BuildServiceProvider();
        await using (var db = await provider.GetRequiredService<IDbContextFactory<FinanceSqliteDbContext>>().CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        var subscriber = provider.GetRequiredService<ArticleAcheteFinanceSubscriber>();
        await subscriber.StartAsync(CancellationToken.None);

        return new FinanceTestFixture(
            connection,
            provider,
            provider.GetRequiredService<FinanceService>(),
            provider.GetRequiredService<IEvenementBus>(),
            provider.GetRequiredService<IDbContextFactory<FinanceDbContext>>(),
            provider.GetRequiredService<TestModuleGuard>(),
            provider.GetRequiredService<TestLiaisonQuery>(),
            provider.GetRequiredService<TestCourseParametres>());
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    internal sealed class TestModuleGuard : IActiveModuleGuard
    {
        public bool FinanceActive { get; set; } = true;

        public Task<bool> IsModuleActiveAsync(string moduleKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(moduleKey == FinanceModule.ModuleKey && FinanceActive);
    }

    internal sealed class TestLiaisonQuery : IModuleLiaisonQuery
    {
        public bool CourseFinanceActive { get; set; }

        public Task<bool> IsLiaisonActiveAsync(string moduleA, string moduleB, CancellationToken cancellationToken = default) =>
            Task.FromResult(CourseFinanceActive);
    }

    internal sealed class TestCourseParametres : ICourseParametresQuery
    {
        public int? CompteId { get; set; }

        public Task<int?> GetCompteCoursesParDefautIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CompteId);
    }
}
