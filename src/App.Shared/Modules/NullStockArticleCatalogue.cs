namespace App.Shared.Modules;

/// <summary>Catalogue Stock vide tant que le module Stock n'est pas enregistré.</summary>
public sealed class NullStockArticleCatalogue : IStockArticleCatalogue
{
    public Task<IReadOnlyList<StockArticleRef>> ListerAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StockArticleRef>>([]);
}
