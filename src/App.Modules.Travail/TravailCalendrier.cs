namespace App.Modules.Travail;

/// <summary>
/// Jours ouvrés et jours fériés français. Sert au pré-remplissage du formulaire de congé
/// et au planning mensuel.
/// </summary>
internal static class TravailCalendrier
{
    /// <summary>Couleur dédiée aux jours fériés sur le calendrier (violet clair).</summary>
    public const string CouleurJourFerie = "#d4c4f0";

    /// <summary>Couleur des jours de pont suggérés (orange/doré).</summary>
    public const string CouleurCongeOpti = "#f0c14a";

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

    /// <summary>Date de Pâques (dimanche) via l'algorithme de Meeus/Jones/Butcher.</summary>
    public static DateTime DatePaques(int annee)
    {
        var a = annee % 19;
        var b = annee / 100;
        var c = annee % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mois = (h + l - 7 * m + 114) / 31;
        var jour = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(annee, mois, jour);
    }

    /// <summary>Jours fériés français (métropole) pour une année civile.</summary>
    public static IReadOnlyList<JourFerieInfo> ObtenirJoursFeries(int annee)
    {
        var paques = DatePaques(annee);
        var feries = new List<JourFerieInfo>
        {
            new(new DateTime(annee, 1, 1), "1er janvier"),
            new(paques.AddDays(1), "Lundi de Pâques"),
            new(new DateTime(annee, 5, 1), "1er mai"),
            new(new DateTime(annee, 5, 8), "8 mai"),
            new(paques.AddDays(39), "Ascension"),
            new(paques.AddDays(50), "Lundi de Pentecôte"),
            new(new DateTime(annee, 7, 14), "14 juillet"),
            new(new DateTime(annee, 8, 15), "15 août"),
            new(new DateTime(annee, 11, 1), "1er novembre"),
            new(new DateTime(annee, 11, 11), "11 novembre"),
            new(new DateTime(annee, 12, 25), "25 décembre")
        };

        return feries.OrderBy(f => f.Date).ToList();
    }

    /// <summary>
    /// Opportunités de pont autour des jours fériés (hors lundi/vendredi déjà accolés au week-end).
    /// Un jour déjà férié ou week-end n'est pas compté comme jour à poser.
    /// </summary>
    public static IReadOnlyList<OpportunitePont> ObtenirOpportunitesPont(int annee)
    {
        var feries = ObtenirJoursFeries(annee);
        var feriesSet = feries.Select(f => f.Date.Date).ToHashSet();
        var resultats = new List<OpportunitePont>();

        foreach (var ferie in feries)
        {
            var date = ferie.Date.Date;
            switch (date.DayOfWeek)
            {
                case DayOfWeek.Tuesday:
                    TryAjouterPont(
                        resultats, ferie, feriesSet,
                        candidats: [date.AddDays(-1)],
                        debutRepos: date.AddDays(-3),
                        finRepos: date);
                    break;

                case DayOfWeek.Thursday:
                    TryAjouterPont(
                        resultats, ferie, feriesSet,
                        candidats: [date.AddDays(1)],
                        debutRepos: date,
                        finRepos: date.AddDays(3));
                    break;

                case DayOfWeek.Wednesday:
                    TryAjouterPont(
                        resultats, ferie, feriesSet,
                        candidats: [date.AddDays(-2), date.AddDays(-1)],
                        debutRepos: date.AddDays(-4),
                        finRepos: date);
                    TryAjouterPont(
                        resultats, ferie, feriesSet,
                        candidats: [date.AddDays(1), date.AddDays(2)],
                        debutRepos: date,
                        finRepos: date.AddDays(4));
                    break;

                // Lundi / vendredi déjà collés au week-end ; samedi / dimanche : rien à pontifier.
            }
        }

        return resultats
            .OrderByDescending(o => o.Efficacite)
            .ThenBy(o => o.JoursAPoser[0])
            .ToList();
    }

    private static void TryAjouterPont(
        List<OpportunitePont> resultats,
        JourFerieInfo ferie,
        HashSet<DateTime> feriesSet,
        IReadOnlyList<DateTime> candidats,
        DateTime debutRepos,
        DateTime finRepos)
    {
        var aPoser = candidats
            .Select(d => d.Date)
            .Where(d => !EstJourNonTravaille(d, feriesSet))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (aPoser.Count == 0)
        {
            return;
        }

        var joursConges = aPoser.Count;
        var joursRepos = (finRepos.Date - debutRepos.Date).Days + 1;
        if (joursRepos <= 0 || joursConges <= 0)
        {
            return;
        }

        var efficacite = decimal.Round((decimal)joursRepos / joursConges, 2);
        resultats.Add(new OpportunitePont(
            ferie.Date.Date,
            ferie.Libelle,
            aPoser,
            joursConges,
            joursRepos,
            efficacite));
    }

    private static bool EstJourNonTravaille(DateTime date, HashSet<DateTime> feriesSet) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
        || feriesSet.Contains(date.Date);
}

/// <summary>Jour férié français avec libellé.</summary>
public sealed record JourFerieInfo(DateTime Date, string Libelle);

/// <summary>Opportunité de pont : jours à poser pour maximiser le repos autour d'un férié.</summary>
public sealed record OpportunitePont(
    DateTime JourFerie,
    string LibelleJourFerie,
    IReadOnlyList<DateTime> JoursAPoser,
    int JoursCongesNecessaires,
    int JoursReposTotal,
    decimal Efficacite,
    bool DejaPlanifie = false);
