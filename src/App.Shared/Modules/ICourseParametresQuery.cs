namespace App.Shared.Modules;

/// <summary>Paramètres Courses utiles aux liaisons (compte Finance par défaut).</summary>
public interface ICourseParametresQuery
{
    Task<int?> GetCompteCoursesParDefautIdAsync(CancellationToken cancellationToken = default);
}
