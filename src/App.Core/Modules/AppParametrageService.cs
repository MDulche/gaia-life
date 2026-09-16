using App.Core.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Modules;

/// <summary>Lecture / écriture du paramétrage global (mode local ou serveur).</summary>
public sealed class AppParametrageService(IDbContextFactory<AppDbContext> dbFactory) : IAppParametrageQuery
{
    public async Task<ModeStockage> GetModeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await ObtenirOuCreerAsync(db, cancellationToken);
        return row.Mode;
    }

    public async Task<string?> GetEmplacementStockageAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await ObtenirOuCreerAsync(db, cancellationToken);
        return row.Mode == ModeStockage.Serveur
            ? NullIfWhiteSpace(row.UrlServeur)
            : NullIfWhiteSpace(row.DossierLocal);
    }

    public async Task<bool> AreIntegrationsIaDisponiblesAsync(CancellationToken cancellationToken = default)
    {
        var mode = await GetModeAsync(cancellationToken);
        return mode == ModeStockage.Serveur;
    }

    public async Task<AppParametrage> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await ObtenirOuCreerAsync(db, cancellationToken);
    }

    public async Task SaveAsync(
        ModeStockage mode,
        string? dossierLocal,
        string? urlServeur,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await ObtenirOuCreerAsync(db, cancellationToken);
        row.Mode = mode;
        row.DossierLocal = NullIfWhiteSpace(dossierLocal);
        row.UrlServeur = NullIfWhiteSpace(urlServeur);

        if (mode == ModeStockage.Local && row.DossierLocal is not null)
        {
            ValidateLocalPath(row.DossierLocal);
        }

        if (mode == ModeStockage.Serveur && row.UrlServeur is not null)
        {
            ValidateUrl(row.UrlServeur);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Vérifie qu'un chemin local existe (ou peut être créé) sur la machine hôte.</summary>
    public static string VerifierDossierLocal(string? chemin)
    {
        if (string.IsNullOrWhiteSpace(chemin))
        {
            throw new InvalidOperationException("Indiquez un chemin de dossier.");
        }

        var full = Path.GetFullPath(chemin.Trim());
        if (Directory.Exists(full))
        {
            return full;
        }

        Directory.CreateDirectory(full);
        return full;
    }

    private static async Task<AppParametrage> ObtenirOuCreerAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var row = await db.AppParametres.FirstOrDefaultAsync(cancellationToken);
        if (row is not null)
        {
            return row;
        }

        row = new AppParametrage { Mode = ModeStockage.Local };
        db.AppParametres.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private static void ValidateLocalPath(string chemin)
    {
        try
        {
            _ = Path.GetFullPath(chemin);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Chemin de dossier invalide : {ex.Message}");
        }
    }

    private static void ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("L'URL serveur doit commencer par http:// ou https://.");
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
