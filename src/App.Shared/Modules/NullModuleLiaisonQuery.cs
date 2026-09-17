namespace App.Shared.Modules;

/// <summary>Aucune liaison active (modules Finance/Stock non portés ou absents du DI).</summary>
public sealed class NullModuleLiaisonQuery : IModuleLiaisonQuery
{
    public Task<bool> IsLiaisonActiveAsync(
        string moduleA,
        string moduleB,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
