namespace App.Modules.Travail.Entities;

/// <summary>Couleur du camembert pour un <see cref="TypeConge"/> (enum, pas une entité métier).</summary>
public class CouleurTypeConge
{
    public int Id { get; set; }

    public TypeConge Type { get; set; }

    public string Couleur { get; set; } = "#0d6efd";
}
