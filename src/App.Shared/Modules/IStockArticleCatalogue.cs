namespace App.Shared.Modules;

/// <summary>Catalogue d'articles Stock exposé aux autres modules (liaison Courses).</summary>
public interface IStockArticleCatalogue
{
    Task<IReadOnlyList<StockArticleRef>> ListerAsync(CancellationToken cancellationToken = default);
}

public sealed record StockArticleRef(int Id, string Nom);
