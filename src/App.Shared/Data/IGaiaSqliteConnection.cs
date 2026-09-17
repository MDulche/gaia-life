namespace App.Shared.Data;

/// <summary>Chaîne SQLite unique (gaialife.db) injectée par l'hôte mobile.</summary>
public interface IGaiaSqliteConnection
{
    string ConnectionString { get; }
}

/// <summary>Implémentation simple enregistrée en singleton dans App.Mobile.</summary>
public sealed class GaiaSqliteConnection(string connectionString) : IGaiaSqliteConnection
{
    public string ConnectionString { get; } =
        string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentException("La chaîne SQLite est obligatoire.", nameof(connectionString))
            : connectionString;
}
