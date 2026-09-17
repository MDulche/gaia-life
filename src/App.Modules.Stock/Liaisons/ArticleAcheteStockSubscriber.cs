using System.Globalization;
using App.Modules.Stock.Services;
using App.Shared.Events;
using App.Shared.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App.Modules.Stock.Liaisons;

/// <summary>Abonne Stock à <see cref="ArticleAcheteEvent"/> (liaison Course|Stock).</summary>
public sealed class ArticleAcheteStockSubscriber : IHostedService
{
    private const string CourseModuleKey = "course";

    private readonly IEvenementBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ArticleAcheteStockSubscriber> _logger;

    public ArticleAcheteStockSubscriber(
        IEvenementBus bus,
        IServiceScopeFactory scopeFactory,
        ILogger<ArticleAcheteStockSubscriber> logger)
    {
        _bus = bus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _bus.Abonner<ArticleAcheteEvent>(OnArticleAchete);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OnArticleAchete(ArticleAcheteEvent evenement)
    {
        try
        {
            HandleAsync(evenement).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la liaison Course→Stock pour l'article {ArticleId}.", evenement.ArticleId);
        }
    }

    private async Task HandleAsync(ArticleAcheteEvent evenement)
    {
        if (evenement.ArticleStockId is not int stockId)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var liaisons = scope.ServiceProvider.GetRequiredService<IModuleLiaisonQuery>();
        if (!await liaisons.IsLiaisonActiveAsync(CourseModuleKey, StockModule.ModuleKey))
        {
            return;
        }

        var delta = ParserDeltaQuantite(evenement.Quantite);
        if (delta == 0)
        {
            return;
        }

        var stock = scope.ServiceProvider.GetRequiredService<StockService>();
        try
        {
            await stock.AjusterQuantiteAsync(stockId, delta, MotifsMouvementStock.AchatCourses);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase))
        {
            // Article Stock absent : aucun effet, pas d'erreur remontée à Courses.
        }
    }

    internal static decimal ParserDeltaQuantite(string? quantite)
    {
        if (string.IsNullOrWhiteSpace(quantite))
        {
            return 1m;
        }

        var trimmed = quantite.Trim();
        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var inv) && inv != 0)
        {
            return inv;
        }

        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.GetCultureInfo("fr-FR"), out var fr) && fr != 0)
        {
            return fr;
        }

        return 1m;
    }
}
