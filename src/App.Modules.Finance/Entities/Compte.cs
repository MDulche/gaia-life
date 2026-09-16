namespace App.Modules.Finance.Entities;

/// <summary>Compte bancaire du foyer. Le solde affiché = <see cref="SoldeInitial"/> ± transactions.</summary>
public class Compte
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public decimal SoldeInitial { get; set; }

    /// <summary>Compte mis en avant en premier sur /finance. Un seul à la fois.</summary>
    public bool EstPrincipal { get; set; }

    public DateTime DateCreation { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public ICollection<ChargeMensuelle> ChargesMensuelles { get; set; } = new List<ChargeMensuelle>();
}
