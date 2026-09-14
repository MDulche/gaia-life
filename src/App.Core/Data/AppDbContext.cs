using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Data;

public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleActivation> ModuleActivations => Set<ModuleActivation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ModuleActivation>(entity =>
        {
            entity.ToTable("ModuleActivation");
            entity.HasIndex(e => e.ModuleKey).IsUnique();
            entity.Property(e => e.ModuleKey).HasMaxLength(64).IsRequired();
        });
    }
}
