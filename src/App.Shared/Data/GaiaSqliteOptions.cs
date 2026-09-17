namespace App.Shared.Data;

/// <summary>
/// Chaîne SQLite partagée ({AppData}/gaialife.db) injectée par App.Mobile.
/// Présente ⇒ les modules utilisent UseSqlite ; absente ⇒ chaîne IConfiguration (archive web MariaDB).
/// </summary>
public sealed class GaiaSqliteOptions
{
    public const string FileName = "gaialife.db";

    public required string ConnectionString { get; init; }
}
