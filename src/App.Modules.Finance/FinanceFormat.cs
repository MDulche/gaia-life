using System.Globalization;
using App.Modules.Finance.Entities;

namespace App.Modules.Finance;

/// <summary>Formatage fr-FR pour les montants et mois affichés dans l'UI Finances.</summary>
internal static class FinanceFormat
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Euro(decimal value) => $"{value.ToString("N2", French)} €";

    public static string Signed(TypeTransaction type, decimal montant)
    {
        var sign = type == TypeTransaction.Entree ? "+" : "−";
        return $"{sign} {Euro(montant)}";
    }

    public static string Mois(DateTime mois) => mois.ToString("MMM yyyy", French);

    public static string NomMois(int mois)
    {
        if (mois is < 1 or > 12)
        {
            return mois.ToString(French);
        }

        return new DateTime(2000, mois, 1).ToString("MMMM", French);
    }

    public static string TypeCompteNom(TypeCompte type) => type switch
    {
        TypeCompte.Epargne => "Épargne",
        _ => "Courant"
    };

    public static string Pourcent(decimal value) => $"{value.ToString("N1", French)} %";

    public static string MoisAnnee(DateTime date) => date.ToString("MMMM yyyy", French);

    /// <summary>Abréviation courte pour les mini-barres (JANV, FEV, MAR…).</summary>
    public static string MoisAbrege(DateTime mois)
    {
        var index = mois.Month - 1;
        return index >= 0 && index < Abreviations.Length
            ? Abreviations[index]
            : mois.ToString("MMM", French).ToUpperInvariant();
    }

    public static string SigneCompact(decimal value)
    {
        var sign = value < 0 ? "−" : "+";
        return sign + Math.Abs(value).ToString("N0", French);
    }

    public static string EuroSigne(decimal value)
    {
        if (value == 0)
        {
            return Euro(0);
        }

        var sign = value > 0 ? "+" : "−";
        return sign + Euro(Math.Abs(value));
    }

    private static readonly string[] Abreviations =
        ["JANV", "FEV", "MAR", "AVR", "MAI", "JUIN", "JUIL", "AOUT", "SEPT", "OCT", "NOV", "DEC"];
}
