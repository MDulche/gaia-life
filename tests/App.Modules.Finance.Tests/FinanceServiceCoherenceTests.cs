using App.Modules.Finance.Entities;
using App.Modules.Finance.Liaisons;
using App.Modules.Finance.Services;
using App.Shared.Events;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Finance.Tests;

public sealed class FinanceServiceCoherenceTests
{
    [Fact]
    public async Task AjouterTransactionAsync_PersisteEtMetAJourLeSolde()
    {
        await using var fx = await FinanceTestFixture.CreateAsync();
        var compte = await fx.Finance.AjouterCompteAsync("Courant", 100m);

        await fx.Finance.AjouterTransactionAsync(new Transaction
        {
            CompteId = compte.Id,
            Date = DateTime.Today,
            Montant = 25m,
            Type = TypeTransaction.Sortie,
            Categorie = "Courses"
        });

        var solde = await fx.Finance.SoldeActuelAsync(compte.Id);
        Assert.Equal(75m, solde);
    }

    [Fact]
    public async Task EffectuerVirementAsync_CreeDeuxJambesLieesExcluesDesTotaux()
    {
        await using var fx = await FinanceTestFixture.CreateAsync();
        var source = await fx.Finance.AjouterCompteAsync("Courant", 500m, TypeCompte.Courant);
        var dest = await fx.Finance.AjouterCompteAsync("Livret", 0m, TypeCompte.Epargne);

        var avant = (await fx.Finance.TotauxMensuelsAsync(1))[^1];
        var (sortie, entree) = await fx.Finance.EffectuerVirementAsync(source.Id, dest.Id, 80m, DateTime.Today);

        Assert.True(sortie.EstVirementInterne);
        Assert.True(entree.EstVirementInterne);
        Assert.Equal(sortie.TransfertId, entree.TransfertId);
        Assert.NotNull(sortie.TransfertId);

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var jambes = await db.Transactions.CountAsync(t => t.TransfertId == sortie.TransfertId);
            Assert.Equal(2, jambes);
        }

        var apres = (await fx.Finance.TotauxMensuelsAsync(1))[^1];
        Assert.Equal(avant.Entrees, apres.Entrees);
        Assert.Equal(avant.Sorties, apres.Sorties);

        Assert.Equal(420m, await fx.Finance.SoldeActuelAsync(source.Id));
        Assert.Equal(80m, await fx.Finance.SoldeActuelAsync(dest.Id));
    }

    [Fact]
    public async Task SupprimerCategorieAsync_BloqueSiUtilisee_PuisReassigne()
    {
        await using var fx = await FinanceTestFixture.CreateAsync();
        var compte = await fx.Finance.AjouterCompteAsync("Courant", 0m);
        await fx.Finance.AjouterCategorieAsync("Vacances", "#ff0");
        await fx.Finance.AjouterTransactionAsync(new Transaction
        {
            CompteId = compte.Id,
            Date = DateTime.Today,
            Montant = 10m,
            Type = TypeTransaction.Sortie,
            Categorie = "Vacances"
        });

        var cat = (await fx.Finance.ListerCategoriesAsync()).Single(c => c.Nom == "Vacances");
        var ex = await Assert.ThrowsAsync<CategorieEncoreUtiliseeException>(() =>
            fx.Finance.SupprimerCategorieAsync(cat.Id));
        Assert.Equal(1, ex.NombreTransactions);

        await fx.Finance.ReassignerEtSupprimerCategorieAsync(cat.Id);
        Assert.DoesNotContain(await fx.Finance.ListerCategoriesAsync(), c => c.Nom == "Vacances");
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Transactions.CountAsync(t => t.Categorie == FinanceService.CategorieNonCategorise));
    }

    [Fact]
    public async Task ArticleAcheteEvent_CreeTransactionQuandConditionsRemplies()
    {
        await using var fx = await FinanceTestFixture.CreateAsync();
        var compte = await fx.Finance.AjouterCompteAsync("Courses", 0m);
        fx.Guard.FinanceActive = true;
        fx.Liaisons.CourseFinanceActive = true;
        fx.CourseParams.CompteId = compte.Id;

        await fx.Bus.PublierAsync(new ArticleAcheteEvent(
            ArticleId: 42,
            ArticleStockId: 7,
            PrixEstime: 12.5m,
            DateAchat: DateTime.Today));

        await using var db = await fx.Factory.CreateDbContextAsync();
        var tx = await db.Transactions.SingleAsync();
        Assert.Equal(12.5m, tx.Montant);
        Assert.Equal(TypeTransaction.Sortie, tx.Type);
        Assert.Equal(ArticleAcheteFinanceSubscriber.CategorieCourses, tx.Categorie);
        Assert.Equal(compte.Id, tx.CompteId);
    }

    [Fact]
    public async Task ArticleAcheteEvent_IgnoreSiPrixOuStockManquantOuLiaisonInactive()
    {
        await using var fx = await FinanceTestFixture.CreateAsync();
        var compte = await fx.Finance.AjouterCompteAsync("Courses", 0m);
        fx.Guard.FinanceActive = true;
        fx.Liaisons.CourseFinanceActive = false;
        fx.CourseParams.CompteId = compte.Id;

        await fx.Bus.PublierAsync(new ArticleAcheteEvent(1, 2, 9m, DateTime.Today));
        await fx.Bus.PublierAsync(new ArticleAcheteEvent(2, null, 9m, DateTime.Today));
        fx.Liaisons.CourseFinanceActive = true;
        await fx.Bus.PublierAsync(new ArticleAcheteEvent(3, 2, null, DateTime.Today));

        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Equal(0, await db.Transactions.CountAsync());
    }
}
