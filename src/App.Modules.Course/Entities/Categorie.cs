namespace App.Modules.Course.Entities;

/// <summary>Catégorie d'article (propre au module Course, distincte de Finance).</summary>
public class Categorie
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public string? Couleur { get; set; }

    public ICollection<ArticleCourse> Articles { get; set; } = new List<ArticleCourse>();
}
