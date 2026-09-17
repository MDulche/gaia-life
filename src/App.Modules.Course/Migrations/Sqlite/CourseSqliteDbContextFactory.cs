using App.Modules.Course.Data;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace App.Modules.Course.Migrations.Sqlite;

/// <summary>
/// Factory design-time pour <c>dotnet ef</c> (migrations SQLite uniquement).
/// Ne pas utiliser la factory MariaDB historique (<c>CourseDbContextFactory</c>, exclue du build).
/// </summary>
public sealed class CourseSqliteDbContextFactory : IDesignTimeDbContextFactory<CourseDbContext>
{
    public CourseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CourseDbContext>();
        optionsBuilder.UseSqlite(GaiaSqlite.BuildConnectionString(Directory.GetCurrentDirectory()));
        return new CourseDbContext(optionsBuilder.Options);
    }
}
