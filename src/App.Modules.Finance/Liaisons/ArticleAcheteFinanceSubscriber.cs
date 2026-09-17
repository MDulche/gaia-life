using App.Modules.Finance.Entities;
using App.Modules.Finance.Services;
using App.Shared.Events;
using App.Shared.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App.Modules.Finance.Liaisons;

/// <summary>
/// Abonne Finance à <see cref="ArticleAcheteEvent"/> (liaison Course|Finance) via le bus async.
/// Ne bloque pas le démarrage si Course / liaisons / paramètres sont absents (null-objects).
/// </summary>
public sealed class ArticleAcheteFinanceSubscriber : IHostedService
{
    public const string CategorieCourses = "Courses";

    private readonly IEvenementBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ArticleAcheteFinanceSubscriber> _logger;

    public ArticleAcheteFinanceSubscriber(
        IEvenementBus bus,
        IServiceScopeFactory scopeFactory,
        ILogger<ArticleAcheteFinanceSubscriber> logger)
    {
        _bus = bus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _bus.Abonner<ArticleAcheteEvent>(OnArticleAcheteAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task OnArticleAcheteAsync(ArticleAcheteEvent evenement)
    {
        try
        {
            await HandleAsync(evenement).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la liaison Course→Finance pour l'article {ArticleId}.", evenement.ArticleId);
            throw;
        }
    }

    private async Task HandleAsync(ArticleAcheteEvent evenement)
    {
        // Contrat mobile : PrixEstime + ArticleStockId obligatoires pour créer une sortie.
        if (evenement.ArticleStockId is null or <= 0)
        {
            return;
        }

        if (evenement.PrixEstime is not decimal prix || prix <= 0)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<IActiveModuleGuard>();
        if (!await guard.IsModuleActiveAsync(FinanceModule.ModuleKey).ConfigureAwait(false))
        {
            return;
        }

        var liaisons = scope.ServiceProvider.GetRequiredService<IModuleLiaisonQuery>();
        if (!await liaisons.IsLiaisonActiveAsync(CourseModuleKey, FinanceModule.ModuleKey).ConfigureAwait(false))
        {
            return;
        }

        var parametres = scope.ServiceProvider.GetRequiredService<ICourseParametresQuery>();
        var compteId = await parametres.GetCompteCoursesParDefautIdAsync().ConfigureAwait(false);
        if (compteId is not int id || id <= 0)
        {
            return;
        }

        var finance = scope.ServiceProvider.GetRequiredService<FinanceService>();
        await finance.EnsureCategorieAsync(CategorieCourses).ConfigureAwait(false);
        await finance.AjouterTransactionAsync(new Transaction
        {
            CompteId = id,
            Date = evenement.DateAchat.Date,
            Montant = decimal.Round(prix, 2),
            Type = TypeTransaction.Sortie,
            Categorie = CategorieCourses,
            Note = $"Courses #{evenement.ArticleId}"
        }).ConfigureAwait(false);
    }

    private const string CourseModuleKey = "course";
}
