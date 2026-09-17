using System.Globalization;
using App.Modules.Travail.Entities;

namespace App.Modules.Travail;

/// <summary>Libellés et badges fr-FR pour types/statuts de congé et montants de paie.</summary>
internal static class TravailFormat
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Euro(decimal value) => $"{value.ToString("N2", French)} €";

    public static string Heures(decimal heures) => $"{heures.ToString("0.##", French)} h";

    public static string Mois(DateTime mois) => mois.ToString("MMMM yyyy", French);

    public static string MoisCourt(DateTime mois) => mois.ToString("MMM", French).ToUpperInvariant();


    public static string Jours(decimal jours) => $"{jours.ToString("0.##", French)} j";

    public static string Type(TypeConge type) => type switch
    {
        TypeConge.Paye => "Payé",
        TypeConge.SansSolde => "Sans solde",
        TypeConge.Maladie => "Maladie",
        TypeConge.RTT => "RTT",
        _ => type.ToString()
    };

    public static string Statut(StatutConge statut) => statut switch
    {
        StatutConge.EnAttente => "En attente",
        StatutConge.Valide => "Validé",
        StatutConge.Refuse => "Refusé",
        _ => statut.ToString()
    };

    public static string BadgeCss(StatutConge statut) => statut switch
    {
        StatutConge.EnAttente => "text-bg-warning",
        StatutConge.Valide => "text-bg-success",
        StatutConge.Refuse => "text-bg-danger",
        _ => "text-bg-secondary"
    };
}
