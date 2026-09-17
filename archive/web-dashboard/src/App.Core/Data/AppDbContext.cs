using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Data;

/// <summary>
/// Contexte Identity + activation des modules.
/// Enregistré à la fois en factory (requêtes Blazor) et en Scoped (stores Identity).
/// </summary>
public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleActivation> ModuleActivations => Set<ModuleActivation>();

    public DbSet<LiaisonModules> LiaisonsModules => Set<LiaisonModules>();

    public DbSet<AppParametrage> AppParametres => Set<AppParametrage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ModuleActivation>(entity =>
        {
            entity.ToTable("ModuleActivation");
            entity.HasIndex(e => e.ModuleKey).IsUnique();
            entity.Property(e => e.ModuleKey).HasMaxLength(64).IsRequired();
        });

        builder.Entity<LiaisonModules>(entity =>
        {
            entity.ToTable("LiaisonModules");
            entity.Property(e => e.ModuleA).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ModuleB).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.ModuleA, e.ModuleB }).IsUnique();
        });

        builder.Entity<AppParametrage>(entity =>
        {
            entity.ToTable("AppParametrage");
            entity.Property(e => e.Mode)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            entity.Property(e => e.DossierLocal).HasMaxLength(512);
            entity.Property(e => e.UrlServeur).HasMaxLength(512);
        });
    }
}
