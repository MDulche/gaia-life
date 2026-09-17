namespace App.Modules.Stock.Entities;

/// <summary>Article du stock foyer (quantité dénormalisée = somme des <see cref="MouvementStock"/>).</summary>
public class ArticleStock
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    /// <summary>Nom normalisé (trim + minuscules) pour unicité insensible à la casse / espaces.</summary>
    public string NomNormalise { get; set; } = string.Empty;

    /// <summary>
    /// Quantité courante, maintenue uniquement via <c>StockService.AjusterQuantiteAsync</c>
    /// (égale à la somme des mouvements).
    /// </summary>
    public decimal Quantite { get; set; }

    /// <summary>Unité libre (ex. kg, unité).</summary>
    public string? Unite { get; set; }

    /// <summary>Sous ce seuil, l'article est considéré en stock bas.</summary>
    public decimal? SeuilAlerte { get; set; }

    public int CategorieId { get; set; }

    public Categorie? Categorie { get; set; }

    public DateTime DateMaj { get; set; }

    public ICollection<MouvementStock> Mouvements { get; set; } = new List<MouvementStock>();
}
