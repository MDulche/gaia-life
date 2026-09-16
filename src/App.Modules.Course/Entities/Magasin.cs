namespace App.Modules.Course.Entities;

/// <summary>Point de passage du parcours de courses. <see cref="Ordre"/> définit la séquence.</summary>
public class Magasin
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public int Ordre { get; set; }

    public ICollection<ArticleCourse> Articles { get; set; } = new List<ArticleCourse>();
}
