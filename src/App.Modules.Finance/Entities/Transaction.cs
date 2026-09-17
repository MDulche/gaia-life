namespace App.Modules.Finance.Entities;

/// <summary>
/// Mouvement sur un compte. <see cref="Montant"/> est toujours positif ;
/// le signe vient de <see cref="Type"/>.
/// Les deux jambes d'un virement interne partagent le même <see cref="TransfertId"/>
/// et ont <see cref="EstVirementInterne"/> à true.
/// </summary>
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

    /// <summary>True pour les mouvements créés par <c>EffectuerVirement</c> (exclus des totaux externes).</summary>
    public bool EstVirementInterne { get; set; }

    /// <summary>Identifiant partagé par les deux transactions d'un même virement ; null sinon.</summary>
    public Guid? TransfertId { get; set; }
}
