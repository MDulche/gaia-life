using App.Modules.Stock.Services;
using App.Shared.Modules;

namespace App.Modules.Stock.Services;

/// <summary>Expose le catalogue Stock aux autres modules.</summary>
public sealed class StockArticleCatalogue(StockService stock) : IStockArticleCatalogue
{
    public async Task<IReadOnlyList<StockArticleRef>> ListerAsync(CancellationToken cancellationToken = default)
    {
        var articles = await stock.ListerArticlesAsync(cancellationToken);
        return articles.Select(a => new StockArticleRef(a.Id, a.Nom)).ToList();
    }
}
