namespace App.Shared.Modules;

/// <summary>Catalogue de comptes Finance exposé aux autres modules (liaison Courses).</summary>
public interface IFinanceCompteCatalogue
{
    Task<IReadOnlyList<FinanceCompteRef>> ListerAsync(CancellationToken cancellationToken = default);
}

public sealed record FinanceCompteRef(int Id, string Nom);
