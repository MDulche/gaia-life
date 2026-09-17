namespace App.Core.Identity;

/// <summary>
/// Rôles Identity du foyer. Ils gouvernent l'admin de l'app (page /admin, validation des congés),
/// pas un cloisonnement des données par utilisateur.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Membre = "Membre";
    public const string Lecture = "Lecture";

    public static readonly string[] All = [Admin, Membre, Lecture];
}
