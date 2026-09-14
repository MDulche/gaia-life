using App.Core.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        foreach (var roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Impossible de créer le rôle '{roleName}' : {FormatErrors(result)}");
                }
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is not null)
        {
            return;
        }

        var admin = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(admin, adminPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Impossible de créer le compte administrateur : {FormatErrors(createResult)}");
        }

        var roleResult = await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Impossible d'assigner le rôle Admin : {FormatErrors(roleResult)}");
        }

        logger.LogInformation("Compte administrateur initial créé pour {Email}.", adminEmail);
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}
