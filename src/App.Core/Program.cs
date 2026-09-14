using System.Globalization;
using App.Core.Data;
using App.Core.Identity;
using App.Core.Modules;
using App.Modules.Finance;
using App.Shared.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("La chaîne de connexion 'Default' est introuvable.");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    AppDbContextConfiguration.Configure(options, connectionString));
builder.Services.AddDbContext<AppDbContext>(
    options => AppDbContextConfiguration.Configure(options, connectionString),
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Singleton);

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

var dataProtectionKeys = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".dataprotection-keys"));
Directory.CreateDirectory(dataProtectionKeys);
builder.Services.AddDataProtection()
    .SetApplicationName("GaiaLife")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeys));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IActiveModuleGuard, ActiveModuleGuard>();

var moduleManager = new ModuleManager();
moduleManager.Register(new FinanceModule());
// Phase 3 : moduleManager.Register(new TravailModule());
moduleManager.ConfigureAllServices(builder.Services);
builder.Services.AddSingleton(moduleManager);

var app = builder.Build();

var french = new CultureInfo("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = french;
CultureInfo.DefaultThreadCurrentUICulture = french;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("fr-FR"),
    SupportedCultures = [french],
    SupportedUICultures = [french]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
if (app.Environment.IsDevelopment())
{
    app.UseDevAutoLogin();
}
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<global::App.Core.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(global::App.Shared.AssemblyMarker).Assembly,
        typeof(FinanceModule).Assembly);

await IdentitySeeder.SeedAsync(app.Services);
await FinanceModule.MigrateAsync(app.Services);

app.Run();
