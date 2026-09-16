namespace App.Modules.Finance.Entities;

/// <summary>
/// Objectif d'épargne du foyer, lié à un compte <see cref="TypeCompte.Epargne"/>.
/// Une seule ligne active à la fois (première version).
/// </summary>
public class ObjectifEpargne
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public decimal MontantCible { get; set; }

    public DateTime? DateCibleOptionnelle { get; set; }

    public int CompteId { get; set; }

    public Compte Compte { get; set; } = default!;
}
