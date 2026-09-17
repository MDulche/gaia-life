namespace App.Shared.Events;

/// <summary>
/// Publié par Courses lors du passage d'un article à l'état acheté (false → true).
/// Contrat figé pour la migration mobile — voir docs/CONTRAT-MIGRATION-MOBILE.md.
/// </summary>
public sealed record ArticleAcheteEvent(
    int ArticleId,
    int? ArticleStockId,
    decimal? PrixEstime,
    DateTime DateAchat);
