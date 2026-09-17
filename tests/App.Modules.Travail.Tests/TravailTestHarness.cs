using App.Modules.Travail.Data;
using App.Modules.Travail.Entities;
using App.Modules.Travail.Services;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Travail.Tests;

/// <summary>Helpers SQLite en mémoire fichier temp pour les tests Travail.</summary>
internal static class TravailTestHarness
{
    public static async Task<T> WithServiceAsync<T>(Func<TravailService, IDbContextFactory<TravailDbContext>, Task<T>> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"travail-test-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<TravailDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using (var db = new TravailDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var factory = new TestFactory(options);
            var service = new TravailService(factory);
            return await action(service, factory);
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    public static Task WithServiceAsync(Func<TravailService, IDbContextFactory<TravailDbContext>, Task> action) =>
        WithServiceAsync(async (s, f) =>
        {
            await action(s, f);
            return 0;
        });

    private sealed class TestFactory(DbContextOptions<TravailDbContext> options)
        : IDbContextFactory<TravailDbContext>
    {
        public TravailDbContext CreateDbContext() => new(options);

        public Task<TravailDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
