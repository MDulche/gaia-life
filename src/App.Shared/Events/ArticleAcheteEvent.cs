namespace App.Shared.Events;

/// <summary>Publié par Courses lors du passage d'un article à l'état acheté (false → true).</summary>
public sealed record ArticleAcheteEvent(
    int ArticleId,
    int? ArticleStockId,
    decimal? PrixEstime,
    string? Quantite);
