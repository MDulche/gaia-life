using App.Modules.Travail.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Travail.Data;

/// <summary>
/// Contexte métier Travail. Factory uniquement (aucun lien Identity).
/// Relations 1-N : Employeur → FichePaies, Conges, SoldeConges, HeuresSupplementaires.
/// </summary>
public class TravailDbContext : DbContext
{
    /// <summary>Constructeur runtime et dérivé SQLite (<see cref="TravailSqliteDbContext"/>).</summary>
    public TravailDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Employeur> Employeurs => Set<Employeur>();

    public DbSet<FichePaie> FichePaies => Set<FichePaie>();

    public DbSet<Conge> Conges => Set<Conge>();

    public DbSet<SoldeConges> SoldeConges => Set<SoldeConges>();

    public DbSet<CouleurTypeConge> CouleursTypes => Set<CouleurTypeConge>();

    public DbSet<HeureSupplementaire> HeuresSupplementaires => Set<HeureSupplementaire>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employeur>(entity =>
        {
            entity.ToTable("Employeurs");
            entity.Property(e => e.Nom).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Adresse).HasMaxLength(256);
            entity.HasMany(e => e.FichePaies)
                .WithOne(e => e.Employeur)
                .HasForeignKey(e => e.EmployeurId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Conges)
                .WithOne(e => e.Employeur)
                .HasForeignKey(e => e.EmployeurId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.SoldesConges)
                .WithOne(e => e.Employeur)
                .HasForeignKey(e => e.EmployeurId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.HeuresSupplementaires)
                .WithOne(e => e.Employeur)
                .HasForeignKey(e => e.EmployeurId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FichePaie>(entity =>
        {
            entity.ToTable("FichePaies");
            entity.Property(e => e.SalaireBrut).HasPrecision(18, 2);
            entity.Property(e => e.SalaireNet).HasPrecision(18, 2);
            entity.Property(e => e.TotalCotisations).HasPrecision(18, 2);
            entity.Property(e => e.CheminFichier).HasMaxLength(512);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasIndex(e => new { e.EmployeurId, e.Mois }).IsUnique();
        });

        modelBuilder.Entity<Conge>(entity =>
        {
            entity.ToTable("Conges");
            entity.Property(e => e.NombreJours).HasPrecision(5, 2);
            entity.Property(e => e.Commentaire).HasMaxLength(500);
            entity.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.Property(e => e.Statut)
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.HasIndex(e => new { e.EmployeurId, e.DateDebut });
        });

        modelBuilder.Entity<SoldeConges>(entity =>
        {
            entity.ToTable("SoldeConges");
            entity.Property(e => e.JoursAcquis).HasPrecision(5, 2);
            entity.HasIndex(e => new { e.EmployeurId, e.Annee }).IsUnique();
        });

        modelBuilder.Entity<CouleurTypeConge>(entity =>
        {
            entity.ToTable("CouleurTypeConges");
            entity.Property(e => e.Couleur).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.HasIndex(e => e.Type).IsUnique();
        });

        modelBuilder.Entity<HeureSupplementaire>(entity =>
        {
            entity.ToTable("HeuresSupplementaires");
            entity.Property(e => e.Contexte).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DureeCalculee).HasPrecision(8, 2);
            entity.HasIndex(e => new { e.EmployeurId, e.Date });
        });
    }
}
