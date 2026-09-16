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

    public bool Achete { get; set; }

    public DateTime DateAjout { get; set; }

    public DateTime? DateAchat { get; set; }
}
