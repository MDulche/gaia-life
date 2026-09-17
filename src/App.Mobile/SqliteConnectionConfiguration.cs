using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace App.Mobile;

/// <summary>
/// Configuration minimale (ConnectionStrings:Default) sans package Configuration.Memory
/// (indisponible sur la source NuGet de cet environnement).
/// </summary>
internal sealed class SqliteConnectionConfiguration(string sqliteConnectionString) : IConfiguration
{
    private readonly string _sqliteConnectionString = sqliteConnectionString;

    public string? this[string key]
    {
        get => IsDefaultConnectionString(key) ? _sqliteConnectionString : null;
        set { }
    }

    public IEnumerable<IConfigurationSection> GetChildren() =>
    [
        new Section(this, "ConnectionStrings")
    ];

    public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);

    public IConfigurationSection GetSection(string key) => new Section(this, key);

    private static bool IsDefaultConnectionString(string key) =>
        string.Equals(key, "ConnectionStrings:Default", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "Default", StringComparison.OrdinalIgnoreCase);

    private sealed class Section(SqliteConnectionConfiguration root, string path) : IConfigurationSection
    {
        public string? this[string key]
        {
            get => root[Combine(path, key)];
            set { }
        }

        public string Key => path.Contains(':') ? path[(path.LastIndexOf(':') + 1)..] : path;

        public string Path => path;

        public string? Value
        {
            get => string.Equals(path, "ConnectionStrings:Default", StringComparison.OrdinalIgnoreCase)
                ? root._sqliteConnectionString
                : null;
            set { }
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            if (string.Equals(path, "ConnectionStrings", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Section(root, "ConnectionStrings:Default");
            }
        }

        public IChangeToken GetReloadToken() => root.GetReloadToken();

        public IConfigurationSection GetSection(string key) => new Section(root, Combine(path, key));

        private static string Combine(string path, string key) => string.IsNullOrEmpty(path) ? key : $"{path}:{key}";
    }
}
