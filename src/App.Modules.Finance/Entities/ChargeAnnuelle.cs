namespace App.Modules.Finance.Entities;

/// <summary>Charge annuelle avec un mois d'échéance (1 = janvier … 12 = décembre).</summary>
public class ChargeAnnuelle
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public decimal Montant { get; set; }

    public int MoisEcheance { get; set; }
}
