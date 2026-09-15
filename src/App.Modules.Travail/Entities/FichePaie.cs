namespace App.Modules.Travail.Entities;

/// <summary>
/// Bulletin de paie. <see cref="Mois"/> est toujours le 1er du mois.
/// <see cref="CheminFichier"/> est prévu pour un futur upload PDF.
/// </summary>
public class FichePaie
{
    public int Id { get; set; }

    public int EmployeurId { get; set; }

    public Employeur Employeur { get; set; } = default!;

    public DateTime Mois { get; set; }

    public decimal SalaireBrut { get; set; }

    public decimal SalaireNet { get; set; }

    public decimal TotalCotisations { get; set; }

    public DateTime DateEmission { get; set; }

    public string? CheminFichier { get; set; }

    public string? Note { get; set; }
}
