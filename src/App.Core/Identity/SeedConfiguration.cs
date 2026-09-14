namespace App.Core.Identity;

internal static class SeedConfiguration
{
    public static string? GetAdminEmail(IConfiguration configuration) =>
        FirstNonEmpty(configuration["SeedAdmin:Email"], configuration["SEED_ADMIN_EMAIL"]);

    public static string? GetAdminPassword(IConfiguration configuration) =>
        FirstNonEmpty(configuration["SeedAdmin:Password"], configuration["SEED_ADMIN_PASSWORD"]);

    public static string? GetTestEmail(IConfiguration configuration) =>
        FirstNonEmpty(configuration["SeedTest:Email"], configuration["SEED_TEST_EMAIL"]);

    public static string? GetTestPassword(IConfiguration configuration) =>
        FirstNonEmpty(configuration["SeedTest:Password"], configuration["SEED_TEST_PASSWORD"]);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));
}
