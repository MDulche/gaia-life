using System.Globalization;
using App.Modules.Finance.Entities;

namespace App.Modules.Finance;

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
}
