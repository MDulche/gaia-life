using App.Modules.Finance.Entities;
using App.Modules.Finance.Services;
using App.Shared.Modules;

namespace App.Modules.Finance.Services;

/// <summary>Expose les comptes Finance aux autres modules.</summary>
public sealed class FinanceCompteCatalogue(FinanceService finance) : IFinanceCompteCatalogue
{
    public async Task<IReadOnlyList<FinanceCompteRef>> ListerAsync(CancellationToken cancellationToken = default)
    {
        var comptes = await finance.ListerComptesAsync(cancellationToken);
        return comptes.Select(c => new FinanceCompteRef(c.Id, c.Nom)).ToList();
    }
}
