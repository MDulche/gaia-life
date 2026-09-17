using System.Security.Claims;
using App.Modules.Travail.Data;
using App.Modules.Travail.Entities;
using App.Modules.Travail.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Travail.Tests;

public sealed class ListerSoldesEmployeursActifsTests
{
    [Fact]
    public async Task Retourne_soldes_agreges_pour_plusieurs_employeurs_actifs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"travail-soldes-{Guid.NewGuid():N}.db");
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
            var service = new TravailService(factory, new FakeAuthStateProvider());

            await using (var db = await factory.CreateDbContextAsync())
            {
                var a = new Employeur { Nom = "Alpha", DateDebut = new DateTime(2024, 1, 1) };
                var b = new Employeur { Nom = "Beta", DateDebut = new DateTime(2025, 1, 1) };
                db.Employeurs.AddRange(a, b);
                await db.SaveChangesAsync();

                var annee = DateTime.Today.Year;
                db.SoldeConges.AddRange(
                    new SoldeConges { EmployeurId = a.Id, Annee = annee, JoursAcquis = 25 },
                    new SoldeConges { EmployeurId = b.Id, Annee = annee, JoursAcquis = 20 });
                db.Conges.Add(new Conge
                {
                    EmployeurId = a.Id,
                    Type = TypeConge.Paye,
                    Statut = StatutConge.Valide,
                    DateDebut = new DateTime(annee, 3, 3),
                    DateFin = new DateTime(annee, 3, 7),
                    NombreJours = 5
                });
                await db.SaveChangesAsync();
            }

            var rows = await service.ListerSoldesEmployeursActifsAsync();
            Assert.Equal(2, rows.Count);
            // Beta a DateDebut plus récente → premier (EstPrincipal).
            Assert.Equal("Beta", rows[0].Nom);
            Assert.True(rows[0].EstPrincipal);
            Assert.Equal(20m, rows[0].JoursRestants);
            Assert.Equal("Alpha", rows[1].Nom);
            Assert.Equal(20m, rows[1].JoursRestants); // 25 - 5 jours ouvrés mars
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }
    }

    private sealed class TestFactory(DbContextOptions<TravailDbContext> options)
        : IDbContextFactory<TravailDbContext>
    {
        public TravailDbContext CreateDbContext() => new(options);

        public Task<TravailDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
