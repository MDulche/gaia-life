namespace App.Modules.Stock.Entities;

/// <summary>Mouvement de quantité (source de vérité ; la quantité affichée = somme des deltas).</summary>
public class MouvementStock
{
    public int Id { get; set; }

    public int ArticleStockId { get; set; }

    public ArticleStock? Article { get; set; }

    /// <summary>Variation positive (entrée) ou négative (sortie).</summary>
    public decimal Delta { get; set; }

    /// <summary>Ex. « Achat Courses », « Ajustement inventaire », « Correction manuelle ».</summary>
    public string Motif { get; set; } = string.Empty;

    public DateTime DateMouvement { get; set; }
}
