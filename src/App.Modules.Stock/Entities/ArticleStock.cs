namespace App.Modules.Stock.Entities;

/// <summary>Article du stock foyer (quantité, seuil d'alerte optionnel).</summary>
public class ArticleStock
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public decimal Quantite { get; set; }

    /// <summary>Unité libre (ex. kg, unité).</summary>
    public string? Unite { get; set; }

    /// <summary>Sous ce seuil, l'article est considéré en stock bas.</summary>
    public decimal? SeuilAlerte { get; set; }

    public int CategorieId { get; set; }

    public Categorie? Categorie { get; set; }

    public DateTime DateMaj { get; set; }
}
