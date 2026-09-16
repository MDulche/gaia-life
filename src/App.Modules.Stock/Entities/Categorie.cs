namespace App.Modules.Stock.Entities;

/// <summary>Catégorie d'article Stock (propre au module, distincte de Course/Finance).</summary>
public class Categorie
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public string? Couleur { get; set; }

    public ICollection<ArticleStock> Articles { get; set; } = new List<ArticleStock>();
}
