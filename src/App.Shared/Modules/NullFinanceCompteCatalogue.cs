namespace App.Shared.Modules;

/// <summary>Catalogue Finance vide tant que le module Finance n'est pas enregistré.</summary>
public sealed class NullFinanceCompteCatalogue : IFinanceCompteCatalogue
{
    public Task<IReadOnlyList<FinanceCompteRef>> ListerAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FinanceCompteRef>>([]);
}
