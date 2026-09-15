using System.ComponentModel.DataAnnotations.Schema;

namespace App.Modules.Travail.Entities;

/// <summary>Jours acquis d'une année pour un employeur. Une ligne unique par (employeur, année).</summary>
public class SoldeConges
{
    public int Id { get; set; }

    public int EmployeurId { get; set; }

    public Employeur Employeur { get; set; } = default!;

    public int Annee { get; set; }

    public decimal JoursAcquis { get; set; }

    /// <summary>
    /// Calculé à partir des congés <see cref="StatutConge.Valide"/> de l'année, non persisté
    /// (évite un décalage avec les validations / refus).
    /// </summary>
    [NotMapped]
    public decimal JoursPris { get; set; }
}
