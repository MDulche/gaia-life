namespace App.Modules.Finance.Entities;

/// <summary>Paramètres du module Finance (une seule ligne attendue).</summary>
public class FinanceParametre
{
    public int Id { get; set; }

    /// <summary>Nombre de jours utilisés pour la moyenne de la prévision de fin de mois.</summary>
    public int JoursMoyennePrevision { get; set; } = 30;
}
