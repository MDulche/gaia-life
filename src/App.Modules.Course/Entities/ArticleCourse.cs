namespace App.Modules.Course.Entities;

/// <summary>Ligne de liste de courses. <see cref="Achete"/> bascule vers l'historique sans supprimer.</summary>
public class ArticleCourse
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public int CategorieId { get; set; }

    public Categorie Categorie { get; set; } = default!;

    public int? MagasinId { get; set; }

    public Magasin? Magasin { get; set; }

    public string? Quantite { get; set; }

    /// <summary>Article Stock lié (pas de FK cross-module) ; utilisé si liaison Course|Stock active.</summary>
    public int? ArticleStockId { get; set; }

    /// <summary>Montant estimé pour la sortie Finance ; utilisé si liaison Course|Finance active.</summary>
    public decimal? PrixEstime { get; set; }

    public bool Achete { get; set; }

    public DateTime DateAjout { get; set; }

    public DateTime? DateAchat { get; set; }
}
