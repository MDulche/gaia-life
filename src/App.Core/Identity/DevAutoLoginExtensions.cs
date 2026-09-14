using Microsoft.AspNetCore.Identity;

namespace App.Core.Identity;

public static class DevAutoLoginExtensions
{
    public static IApplicationBuilder UseDevAutoLogin(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();

            if (!environment.IsDevelopment()
                || !configuration.GetValue("SeedAdmin:AutoLogin", false)
                || context.User.Identity?.IsAuthenticated == true
                || HttpMethods.IsPost(context.Request.Method)
                || ShouldSkip(context.Request.Path))
            {
                await next();
                return;
            }

            var email = configuration["SeedAdmin:Email"];
            var password = configuration["SeedAdmin:Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                await next();
                return;
            }

            var userManager = context.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
            var signInManager = context.RequestServices.GetRequiredService<SignInManager<IdentityUser>>();
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                await next();
                return;
            }

            var result = await signInManager.PasswordSignInAsync(
                user,
                password,
                isPersistent: true,
                lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                await next();
                return;
            }

            context.Response.Redirect(GetRedirectTarget(context));
        });
    }

    private static bool ShouldSkip(PathString path)
    {
        if (path.StartsWithSegments("/_blazor")
            || path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_content")
            || path.StartsWithSegments("/lib")
            || path.StartsWithSegments("/Account/Logout"))
        {
            return true;
        }

        var value = path.Value ?? string.Empty;
        return Path.HasExtension(value);
    }

    private static string GetRedirectTarget(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/Account/Login"))
        {
            return context.Request.Path + context.Request.QueryString;
        }

        var returnUrl = context.Request.Query["ReturnUrl"].ToString();
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl.StartsWith("/Account", StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        return returnUrl.StartsWith('/') ? returnUrl : "/";
    }
}
