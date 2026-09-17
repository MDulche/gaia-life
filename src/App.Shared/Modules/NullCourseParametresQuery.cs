namespace App.Shared.Modules;

/// <summary>Aucun compte Courses par défaut (module Course non porté ou absents du DI).</summary>
public sealed class NullCourseParametresQuery : ICourseParametresQuery
{
    public Task<int?> GetCompteCoursesParDefautIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<int?>(null);
}
