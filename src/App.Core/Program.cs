using System.Globalization;
using App.Core.Data;
using App.Core.Health;
using App.Core.Identity;
using App.Core.Modules;
using App.Modules.Course;
using App.Modules.Finance;
using App.Modules.Travail;
using App.Shared.Modules;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) =>
{
    var isDev = context.HostingEnvironment.IsDevelopment();
    var configuredLogs = context.Configuration[$"{GaiaHealthOptions.SectionName}:LogsPath"] ?? "logs";
    var logsDir = Path.IsPathRooted(configuredLogs)
        ? configuredLogs
        : Path.Combine(context.HostingEnvironment.ContentRootPath, configuredLogs);
    Directory.CreateDirectory(logsDir);

    logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            restrictedToMinimumLevel: isDev ? LogEventLevel.Information : LogEventLevel.Warning,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.File(
            Path.Combine(logsDir, "gaia-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("La chaîne de connexion 'Default' est introuvable.");

builder.Services.Configure<GaiaHealthOptions>(
    builder.Configuration.GetSection(GaiaHealthOptions.SectionName));
builder.Services.PostConfigure<GaiaHealthOptions>(opts =>
{
    var root = builder.Environment.ContentRootPath;
    if (!Path.IsPathRooted(opts.BackupsPath))
    {
        opts.BackupsPath = Path.GetFullPath(Path.Combine(root, opts.BackupsPath));
    }

    if (!Path.IsPathRooted(opts.LogsPath))
    {
        opts.LogsPath = Path.GetFullPath(Path.Combine(root, opts.LogsPath));
    }

    Directory.CreateDirectory(opts.BackupsPath);
    Directory.CreateDirectory(opts.LogsPath);
});
builder.Services.AddSingleton<BackupFolderMonitor>();
builder.Services.AddSingleton<SerilogFileTail>();
builder.Services.AddSingleton<HealthAlertState>();
builder.Services.AddSingleton<IHealthCheckPublisher, LoggingHealthCheckPublisher>();
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    var seconds = builder.Configuration.GetValue<int>($"{GaiaHealthOptions.SectionName}:PublishPeriodSeconds", 15);
    options.Delay = TimeSpan.FromSeconds(5);
    options.Period = TimeSpan.FromSeconds(Math.Max(5, seconds));
});

builder.Services.AddHealthChecks()
    .AddMySql(
        connectionString,
        name: "mariadb",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready", "db"],
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<DiskSpaceHealthCheck>("espace-disque", tags: ["ready"])
    .AddCheck<LastBackupHealthCheck>("derniere-sauvegarde", tags: ["ready"]);

// Factory : pages Blazor / menu / migrations. Scoped + options Singleton : stores Identity
// (UserManager) qui exigent un DbContext par requête HTTP, sans rendre DbContextOptions scoped
// (sinon IDbContextFactory singleton ne peut pas les consommer).
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

// Toute page sans [AllowAnonymous] exige un utilisateur authentifié.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Clés persistées hors du conteneur pour que les cookies d'auth survivent au redémarrage de app-core.
var dataProtectionKeys = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".dataprotection-keys"));
Directory.CreateDirectory(dataProtectionKeys);
builder.Services.AddDataProtection()
    .SetApplicationName("GaiaLife")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeys));

// Nginx (réseau Docker) envoie X-Forwarded-Proto=https : Identity et HSTS
// voient une origine sûre alors que Kestrel n'écoute qu'en HTTP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IActiveModuleGuard, ActiveModuleGuard>();

var moduleManager = new ModuleManager();
moduleManager.Register(new FinanceModule());
moduleManager.Register(new TravailModule());
moduleManager.Register(new CourseModule());
moduleManager.ConfigureAllServices(builder.Services);
builder.Services.AddSingleton(moduleManager);

var app = builder.Build();

var healthOpts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<GaiaHealthOptions>>().Value;
app.Logger.LogInformation(
    "Journalisation Serilog vers {LogsPath}, dumps MariaDB dans {BackupsPath}",
    healthOpts.LogsPath,
    healthOpts.BackupsPath);

app.UseForwardedHeaders();

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
    // HSTS n'a d'effet utile que si le navigateur atteint l'app en HTTPS
    // (reverse proxy Let's Encrypt en prod, profil https hors Docker).
    app.UseHsts();
}

// Dans Docker l'app n'écoute qu'en HTTP : la redirection HTTPS casserait :8080.
// Le TLS se termine sur le reverse proxy (voir docs/DEPLOIEMENT.md).
if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, _, exception) =>
    {
        if (httpContext.Request.Path.StartsWithSegments("/health"))
        {
            return LogEventLevel.Verbose;
        }

        return exception is not null ? LogEventLevel.Error : LogEventLevel.Information;
    };
});

app.UseAuthentication();
if (app.Environment.IsDevelopment())
{
    app.UseDevAutoLogin();
}
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false
}).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    AllowCachingResponses = false,
    ResponseWriter = async (context, _) =>
    {
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("ok");
    }
}).AllowAnonymous();
app.MapRazorComponents<global::App.Core.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(global::App.Shared.AssemblyMarker).Assembly,
        typeof(FinanceModule).Assembly,
        typeof(TravailModule).Assembly,
        typeof(CourseModule).Assembly);

try
{
    await IdentitySeeder.SeedAsync(app.Services);
    await FinanceModule.MigrateAsync(app.Services);
    await TravailModule.MigrateAsync(app.Services);
    await CourseModule.MigrateAsync(app.Services);
    await app.RunAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}
