namespace App.Modules.Finance.Entities;

/// <summary>Catégorie de transaction (nom unique). <see cref="Couleur"/> sert aux graphiques de synthèse.</summary>
public class Categorie
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public string? Couleur { get; set; }
}
