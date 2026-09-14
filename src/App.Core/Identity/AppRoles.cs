namespace App.Core.Identity;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Membre = "Membre";
    public const string Lecture = "Lecture";

    public static readonly string[] All = [Admin, Membre, Lecture];
}
