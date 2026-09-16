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
}
