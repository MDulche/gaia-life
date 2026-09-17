using App.Modules.Finance.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Finance.Data;

/// <summary>
/// Contexte métier Finances. Uniquement via <c>IDbContextFactory</c> (pas de Scoped Identity).
/// </summary>
public class FinanceDbContext : DbContext
{
    public FinanceDbContext(DbContextOptions<FinanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Compte> Comptes => Set<Compte>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Categorie> Categories => Set<Categorie>();

    public DbSet<ChargeMensuelle> ChargesMensuelles => Set<ChargeMensuelle>();

    public DbSet<ChargeAnnuelle> ChargesAnnuelles => Set<ChargeAnnuelle>();

    public DbSet<FinanceParametre> Parametres => Set<FinanceParametre>();

    public DbSet<ObjectifEpargne> ObjectifsEpargne => Set<ObjectifEpargne>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Compte>(entity =>
        {
            entity.ToTable("Comptes");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.SoldeInitial).HasPrecision(18, 2);
            entity.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(TypeCompte.Courant);
            entity.HasMany(e => e.Transactions)
                .WithOne(e => e.Compte)
                .HasForeignKey(e => e.CompteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ChargesMensuelles)
                .WithOne(e => e.Compte)
                .HasForeignKey(e => e.CompteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.ObjectifsEpargne)
                .WithOne(e => e.Compte)
                .HasForeignKey(e => e.CompteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.Property(e => e.Montant).HasPrecision(18, 2);
            entity.Property(e => e.Categorie).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.Property(e => e.EstVirementInterne).HasDefaultValue(false);
            entity.HasIndex(e => new { e.CompteId, e.Date });
            entity.HasIndex(e => e.TransfertId);
            entity.HasIndex(e => e.EstVirementInterne);
        });

        modelBuilder.Entity<Categorie>(entity =>
        {
            entity.ToTable("Categories");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Couleur).HasMaxLength(16);
            entity.HasIndex(e => e.Nom).IsUnique();
        });

        modelBuilder.Entity<ChargeMensuelle>(entity =>
        {
            entity.ToTable("ChargesMensuelles");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Montant).HasPrecision(18, 2);
            entity.HasIndex(e => e.CompteId);
        });

        modelBuilder.Entity<ChargeAnnuelle>(entity =>
        {
            entity.ToTable("ChargesAnnuelles");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Montant).HasPrecision(18, 2);
            entity.Property(e => e.MoisEcheance);
        });

        modelBuilder.Entity<FinanceParametre>(entity =>
        {
            entity.ToTable("FinanceParametres");
        });

        modelBuilder.Entity<ObjectifEpargne>(entity =>
        {
            entity.ToTable("ObjectifsEpargne");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.MontantCible).HasPrecision(18, 2);
            entity.HasIndex(e => e.CompteId);
        });
    }
}
