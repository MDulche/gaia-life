namespace App.Modules.Finance.Entities;

/// <summary>Charge récurrente chaque mois, associée à un compte.</summary>
public class ChargeMensuelle
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public decimal Montant { get; set; }

    public int CompteId { get; set; }

    public Compte Compte { get; set; } = default!;
}
