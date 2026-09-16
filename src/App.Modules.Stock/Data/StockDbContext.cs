using App.Modules.Stock.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Stock.Data;

/// <summary>
/// Contexte métier Stock. Uniquement via <c>IDbContextFactory</c>.
/// Tables préfixées <c>Stock*</c>.
/// </summary>
public class StockDbContext : DbContext
{
    public StockDbContext(DbContextOptions<StockDbContext> options)
        : base(options)
    {
    }

    public DbSet<Categorie> Categories => Set<Categorie>();

    public DbSet<ArticleStock> Articles => Set<ArticleStock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categorie>(entity =>
        {
            entity.ToTable("StockCategories");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Couleur).HasMaxLength(16);
            entity.HasIndex(e => e.Nom).IsUnique();
            entity.HasMany(e => e.Articles)
                .WithOne(e => e.Categorie)
                .HasForeignKey(e => e.CategorieId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ArticleStock>(entity =>
        {
            entity.ToTable("StockArticles");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Quantite).HasPrecision(18, 3);
            entity.Property(e => e.Unite).HasMaxLength(32);
            entity.Property(e => e.SeuilAlerte).HasPrecision(18, 3);
            entity.HasIndex(e => e.Nom);
            entity.HasIndex(e => e.CategorieId);
        });
    }
}
