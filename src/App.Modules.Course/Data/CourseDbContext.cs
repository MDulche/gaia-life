using App.Modules.Course.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Course.Data;

/// <summary>
/// Contexte métier Courses. Uniquement via <c>IDbContextFactory</c> (pas de Scoped Identity).
/// Tables préfixées pour ne pas collisionner avec Finance (<c>Categories</c>).
/// </summary>
public class CourseDbContext : DbContext
{
    public CourseDbContext(DbContextOptions<CourseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Magasin> Magasins => Set<Magasin>();

    public DbSet<Categorie> Categories => Set<Categorie>();

    public DbSet<ArticleCourse> Articles => Set<ArticleCourse>();

    public DbSet<CourseParametre> Parametres => Set<CourseParametre>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Magasin>(entity =>
        {
            entity.ToTable("CourseMagasins");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.Ordre).IsUnique();
            entity.HasMany(e => e.Articles)
                .WithOne(e => e.Magasin)
                .HasForeignKey(e => e.MagasinId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Categorie>(entity =>
        {
            entity.ToTable("CourseCategories");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Couleur).HasMaxLength(16);
            entity.HasIndex(e => e.Nom).IsUnique();
            entity.HasMany(e => e.Articles)
                .WithOne(e => e.Categorie)
                .HasForeignKey(e => e.CategorieId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ArticleCourse>(entity =>
        {
            entity.ToTable("CourseArticles");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Quantite).HasMaxLength(64);
            entity.Property(e => e.PrixEstime).HasPrecision(18, 2);
            entity.HasIndex(e => e.Achete);
            entity.HasIndex(e => new { e.MagasinId, e.Achete });
            entity.HasIndex(e => e.DateAchat);
        });

        modelBuilder.Entity<CourseParametre>(entity =>
        {
            entity.ToTable("CourseParametres");
        });
    }
}
