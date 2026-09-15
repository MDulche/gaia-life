namespace App.Modules.Travail;

/// <summary>
/// Jours ouvrés lun–ven (pas de jours fériés). Sert au pré-remplissage du formulaire de congé ;
/// l'utilisateur peut ensuite ajuster pour une demi-journée.
/// </summary>
internal static class TravailCalendrier
{
    public static decimal CompterJoursOuvres(DateTime debut, DateTime fin)
    {
        if (fin.Date < debut.Date)
        {
            return 0;
        }

        decimal jours = 0;
        for (var jour = debut.Date; jour <= fin.Date; jour = jour.AddDays(1))
        {
            if (jour.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                jours += 1;
            }
        }

        return jours;
    }
}
