using App.Modules.Stock.Liaisons;
using App.Modules.Stock.Services;
using App.Shared.Events;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Modules.Stock.Tests;

public sealed class StockServiceAjusterQuantiteTests
{
    [Fact]
    public async Task AjouterArticle_persiste_quantite_initiale()
    {
        await using var fx = await StockTestFixture.CreateAsync();
        var catId = await fx.SeedCategorieAsync();
        var resultat = await fx.Service.AjouterArticleAsync("Riz", 3.5m, "kg", 1m, catId);

        Assert.True(resultat.Cree);
        Assert.Equal(3.5m, await fx.GetQuantiteAsync(resultat.ArticleId));
        Assert.Equal(1, await fx.CompterMouvementsAsync(resultat.ArticleId));
    }

    [Fact]
    public async Task AjusterQuantite_positif_et_negatif()
    {
        await using var fx = await StockTestFixture.CreateAsync();
        var catId = await fx.SeedCategorieAsync();
        var articleId = await fx.SeedArticleAsync(catId, "Huile", 10m);

        await fx.Service.AjusterQuantiteAsync(articleId, 2m, MotifsMouvementStock.CorrectionManuelle);
        Assert.Equal(12m, await fx.GetQuantiteAsync(articleId));

        await fx.Service.AjusterQuantiteAsync(articleId, -4m, MotifsMouvementStock.CorrectionManuelle);
        Assert.Equal(8m, await fx.GetQuantiteAsync(articleId));
        Assert.Equal(3, await fx.CompterMouvementsAsync(articleId));
    }

    [Fact]
    public async Task AjusterQuantite_deux_appels_concurrents_conservent_les_deux_mouvements()
    {
        await using var fx = await StockTestFixture.CreateAsync();
        var catId = await fx.SeedCategorieAsync();
        var articleId = await fx.SeedArticleAsync(catId, "Lait", 10m);

        await Task.WhenAll(
            fx.Service.AjusterQuantiteAsync(articleId, 3m, MotifsMouvementStock.CorrectionManuelle),
            fx.Service.AjusterQuantiteAsync(articleId, 5m, MotifsMouvementStock.CorrectionManuelle));

        var quantite = await fx.GetQuantiteAsync(articleId);
        var mouvements = await fx.CompterMouvementsAsync(articleId);

        Assert.Equal(18m, quantite);
        Assert.Equal(3, mouvements);
    }

    [Fact]
    public async Task AjusterQuantite_refuse_quantite_negative_hors_inventaire()
    {
        await using var fx = await StockTestFixture.CreateAsync();
        var catId = await fx.SeedCategorieAsync();
        var articleId = await fx.SeedArticleAsync(catId, "Farine", 2m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fx.Service.AjusterQuantiteAsync(articleId, -5m, MotifsMouvementStock.CorrectionManuelle));

        Assert.Contains("négative", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2m, await fx.GetQuantiteAsync(articleId));
        Assert.Equal(1, await fx.CompterMouvementsAsync(articleId));
    }

    [Fact]
    public async Task AjusterQuantite_autorise_negatif_si_ajustement_inventaire()
    {
        await using var fx = await StockTestFixture.CreateAsync();
        var catId = await fx.SeedCategorieAsync();
        var articleId = await fx.SeedArticleAsync(catId, "Sucre", 2m);

        var quantite = await fx.Service.AjusterQuantiteAsync(
            articleId,
            -5m,
            MotifsMouvementStock.AjustementInventaire);

        Assert.Equal(-3m, quantite);
        Assert.Equal(-3m, await fx.GetQuantiteAsync(articleId));
    }

    [Fact]
    public async Task AjouterArticle_nom_existant_propose_fusion_puis_fusionne()
    {
        await using var fx = await StockTestFixture.CreateAsync();
        var catId = await fx.SeedCategorieAsync();
        var premier = await fx.Service.AjouterArticleAsync("  Pâtes  ", 4m, "kg", null, catId);
        Assert.True(premier.Cree);

        var proposition = await fx.Service.AjouterArticleAsync("pâtes", 2m, "kg", null, catId);
        Assert.True(proposition.FusionProposee);
        Assert.False(proposition.Cree);
        Assert.Equal(premier.ArticleId, proposition.ArticleId);
        Assert.Equal(4m, await fx.GetQuantiteAsync(premier.ArticleId));

        var fusion = await fx.Service.AjouterArticleAsync("PÂTES", 2m, "kg", null, catId, fusionnerSiExistant: true);
        Assert.True(fusion.FusionEffectuee);
        Assert.Equal(6m, await fx.GetQuantiteAsync(premier.ArticleId));

        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.Equal(1, await db.Articles.CountAsync());
    }

    [Fact]
    public async Task ArticleAcheteEvent_ajuste_stock_via_AjusterQuantite()
    {
        await using var fx = await StockTestFixture.CreateAsync();
        var catId = await fx.SeedCategorieAsync();
        var articleId = await fx.SeedArticleAsync(catId, "Beurre", 1m);

        var services = new ServiceCollection();
        services.AddSingleton(fx.Factory);
        services.AddScoped<StockService>();
        services.AddSingleton<IModuleLiaisonQuery, AlwaysActiveLiaison>();
        services.AddLogging();
        services.AddSingleton<IEvenementBus>(sp =>
            new EvenementBus(sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EvenementBus>>()));
        services.AddSingleton<ArticleAcheteStockSubscriber>();
        await using var provider = services.BuildServiceProvider();

        var subscriber = provider.GetRequiredService<ArticleAcheteStockSubscriber>();
        await subscriber.StartAsync(CancellationToken.None);

        var bus = provider.GetRequiredService<IEvenementBus>();
        await bus.PublierAsync(new ArticleAcheteEvent(
            ArticleId: 42,
            ArticleStockId: articleId,
            PrixEstime: 2.5m,
            DateAchat: DateTime.UtcNow));

        Assert.Equal(2m, await fx.GetQuantiteAsync(articleId));
        Assert.Equal(2, await fx.CompterMouvementsAsync(articleId));
    }

    private sealed class AlwaysActiveLiaison : IModuleLiaisonQuery
    {
        public Task<bool> IsLiaisonActiveAsync(
            string moduleA,
            string moduleB,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
