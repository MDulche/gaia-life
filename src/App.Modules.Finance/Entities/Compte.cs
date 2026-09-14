namespace App.Modules.Finance.Entities;

public class Compte
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public decimal SoldeInitial { get; set; }

    public DateTime DateCreation { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
