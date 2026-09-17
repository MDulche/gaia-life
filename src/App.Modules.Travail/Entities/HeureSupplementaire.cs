namespace App.Modules.Travail.Entities;

/// <summary>Heures supplémentaires sur une journée (pas de passage à minuit).</summary>
public class HeureSupplementaire
{
    public int Id { get; set; }

    public int EmployeurId { get; set; }

    public Employeur Employeur { get; set; } = default!;

    public DateTime Date { get; set; }

    public TimeSpan HeureDebut { get; set; }

    public TimeSpan HeureFin { get; set; }

    /// <summary>Note libre (ex. Remplacement, Fin de mois).</summary>
    public string Contexte { get; set; } = string.Empty;

    /// <summary>Durée en heures décimales (HeureFin − HeureDebut), calculée à l'enregistrement.</summary>
    public decimal DureeCalculee { get; set; }
}
