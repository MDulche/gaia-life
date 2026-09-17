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

    public DbSet<MouvementStock> Mouvements => Set<MouvementStock>();

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
            // Quantite dénormalisée (= somme des mouvements), maj uniquement via AjusterQuantiteAsync.
            // Pas de CHECK Quantite >= 0 : le motif « Ajustement inventaire » peut laisser un négatif
            // (rattrapage). Garde-fou applicatif dans StockService.AjusterQuantiteAsync.
            entity.ToTable("StockArticles");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.NomNormalise).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Quantite).HasPrecision(18, 3);
            entity.Property(e => e.Unite).HasMaxLength(32);
            entity.Property(e => e.SeuilAlerte).HasPrecision(18, 3);
            entity.HasIndex(e => e.NomNormalise).IsUnique();
            entity.HasIndex(e => e.CategorieId);
            entity.HasMany(e => e.Mouvements)
                .WithOne(e => e.Article)
                .HasForeignKey(e => e.ArticleStockId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MouvementStock>(entity =>
        {
            entity.ToTable("StockMouvements");
            entity.Property(e => e.Delta).HasPrecision(18, 3);
            entity.Property(e => e.Motif).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.ArticleStockId);
            entity.HasIndex(e => e.DateMouvement);
        });
    }
}
