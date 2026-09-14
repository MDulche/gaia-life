namespace App.Modules.Finance.Entities;

public class Transaction
{
    public int Id { get; set; }

    public int CompteId { get; set; }

    public Compte Compte { get; set; } = default!;

    public DateTime Date { get; set; }

    public decimal Montant { get; set; }

    public TypeTransaction Type { get; set; }

    public string Categorie { get; set; } = string.Empty;

    public string? Note { get; set; }
}
