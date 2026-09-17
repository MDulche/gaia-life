namespace App.Modules.Stock.Services;

/// <summary>Motifs reconnus pour <see cref="StockService.AjusterQuantiteAsync"/>.</summary>
public static class MotifsMouvementStock
{
    public const string AchatCourses = "Achat Courses";

    public const string AjustementInventaire = "Ajustement inventaire";

    public const string CorrectionManuelle = "Correction manuelle";

    public const string StockInitial = "Stock initial";

    /// <summary>Seul ce motif autorise une quantité résultante négative.</summary>
    public static bool AutoriseQuantiteNegative(string motif) =>
        string.Equals(motif.Trim(), AjustementInventaire, StringComparison.Ordinal);
}
