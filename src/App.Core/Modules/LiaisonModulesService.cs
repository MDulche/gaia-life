using App.Core.Data;
using App.Shared.Modules;
using Microsoft.EntityFrameworkCore;

namespace App.Core.Modules;

/// <summary>
/// Paires de modules pouvant être liées (état en base uniquement, pas de sync métier).
/// Une paire est créée entre chaque module et le suivant dans l'ordre d'enregistrement.
/// </summary>
public sealed class LiaisonModulesService
{
    public sealed record PairDefinition(
        string ModuleA,
        string ModuleB,
        string Titre,
        string Description);

    /// <summary>Paires adjacentes selon l'ordre du catalogue (Register).</summary>
    public static IReadOnlyList<PairDefinition> BuildAdjacentPairs(IReadOnlyList<IAppModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var pairs = new List<PairDefinition>();
        for (var i = 0; i < modules.Count - 1; i++)
        {
            var left = modules[i];
            var right = modules[i + 1];
            pairs.Add(new PairDefinition(
                left.Key,
                right.Key,
                $"{left.DisplayName} ↔ {right.DisplayName}",
                Describe(left.Key, right.Key)));
        }

        return pairs;
    }

    public async Task<List<LiaisonModules>> ListerAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        return await db.LiaisonsModules.AsNoTracking()
            .OrderBy(l => l.ModuleA)
            .ThenBy(l => l.ModuleB)
            .ToListAsync(cancellationToken);
    }

    public async Task<ISet<string>> GetModuleKeysAvecLiaisonActiveAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var actives = await db.LiaisonsModules.AsNoTracking()
            .Where(l => l.EstActive)
            .ToListAsync(cancellationToken);

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var liaison in actives)
        {
            keys.Add(liaison.ModuleA);
            keys.Add(liaison.ModuleB);
        }

        return keys;
    }

    public async Task SetActiveAsync(
        AppDbContext db,
        string moduleA,
        string moduleB,
        bool estActive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleA);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleB);

        NormalizePair(moduleA, moduleB, out var a, out var b);

        var entity = await db.LiaisonsModules
            .FirstOrDefaultAsync(l => l.ModuleA == a && l.ModuleB == b, cancellationToken);

        if (entity is null)
        {
            db.LiaisonsModules.Add(new LiaisonModules
            {
                ModuleA = a,
                ModuleB = b,
                EstActive = estActive
            });
        }
        else
        {
            entity.EstActive = estActive;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static bool PairVisible(PairDefinition pair, ISet<string> activeKeys) =>
        activeKeys.Contains(pair.ModuleA) && activeKeys.Contains(pair.ModuleB);

    public static bool AnyPairVisible(IReadOnlyList<PairDefinition> pairs, ISet<string> activeKeys) =>
        pairs.Any(p => PairVisible(p, activeKeys));

    public static bool IsPairActive(IEnumerable<LiaisonModules> liaisons, PairDefinition pair)
    {
        NormalizePair(pair.ModuleA, pair.ModuleB, out var a, out var b);
        return liaisons.Any(l =>
            l.EstActive
            && string.Equals(l.ModuleA, a, StringComparison.OrdinalIgnoreCase)
            && string.Equals(l.ModuleB, b, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsActiveAsync(
        AppDbContext db,
        string moduleA,
        string moduleB,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleA);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleB);

        NormalizePair(moduleA, moduleB, out var a, out var b);
        return await db.LiaisonsModules.AsNoTracking()
            .AnyAsync(l => l.EstActive && l.ModuleA == a && l.ModuleB == b, cancellationToken);
    }

    private static string Describe(string moduleA, string moduleB)
    {
        NormalizePair(moduleA, moduleB, out var a, out var b);
        var key = $"{a}|{b}";
        return key switch
        {
            "finance|travail" =>
                "Quand activé : des interactions automatiques entre Travail et Finances (paies, charges liées) — à développer.",
            "course|finance" =>
                "Quand activé : cocher un article acheté dans Courses créera automatiquement une dépense correspondante dans Finances.",
            "course|stock" =>
                "Quand activé : cocher un article acheté dans Courses incrémentera automatiquement sa quantité dans Stock.",
            _ =>
                "Quand activé : interconnexion automatique entre ces deux modules — à développer."
        };
    }

    /// <summary>Ordre canonique alphabétique pour unicité en base.</summary>
    private static void NormalizePair(string moduleA, string moduleB, out string a, out string b)
    {
        var x = moduleA.Trim().ToLowerInvariant();
        var y = moduleB.Trim().ToLowerInvariant();
        if (string.CompareOrdinal(x, y) <= 0)
        {
            a = x;
            b = y;
        }
        else
        {
            a = y;
            b = x;
        }
    }
}
