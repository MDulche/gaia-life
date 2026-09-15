namespace App.Modules.Travail.Entities;

/// <summary>
/// Demande de congé. <see cref="NombreJours"/> accepte les demi-journées (ex. 0,5).
/// Un chevauchement n'est interdit qu'avec un autre congé déjà <see cref="StatutConge.Valide"/>.
/// </summary>
public class Conge
{
    public int Id { get; set; }

    public int EmployeurId { get; set; }

    public Employeur Employeur { get; set; } = default!;

    public TypeConge Type { get; set; }

    public DateTime DateDebut { get; set; }

    public DateTime DateFin { get; set; }

    public decimal NombreJours { get; set; }

    public StatutConge Statut { get; set; }

    public string? Commentaire { get; set; }
}
