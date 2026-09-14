using App.Core.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
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

        if (!environment.IsDevelopment())
        {
            return;
        }

        await EnsureDevelopmentUserAsync(
            userManager,
            logger,
            SeedConfiguration.GetAdminEmail(configuration),
            SeedConfiguration.GetAdminPassword(configuration),
            AppRoles.Admin);

        await EnsureDevelopmentUserAsync(
            userManager,
            logger,
            SeedConfiguration.GetTestEmail(configuration),
            SeedConfiguration.GetTestPassword(configuration),
            AppRoles.Membre);
    }

    private static async Task EnsureDevelopmentUserAsync(
        UserManager<IdentityUser> userManager,
        ILogger logger,
        string? email,
        string? password,
        string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Impossible de créer le compte '{email}' : {FormatErrors(createResult)}");
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Impossible d'assigner le rôle {role} à '{email}' : {FormatErrors(roleResult)}");
        }

        logger.LogInformation("Compte de développement créé pour {Email} ({Role}).", email, role);
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}
