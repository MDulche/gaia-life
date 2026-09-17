using Microsoft.Extensions.DependencyInjection;

namespace App.Shared.Modules;

/// <summary>
/// Contrat d'un module métier (Finances, Travail, …).
/// Enregistré dans <c>ModuleCatalog</c> au démarrage ; l'activation utilisateur est locale (mobile) ou archivée (web).
/// </summary>
public interface IAppModule
{
    /// <summary>Clé technique persistée dans <c>ModuleActivation</c> (ex. <c>finance</c>).</summary>
    string Key { get; }

    /// <summary>Libellé affiché dans le menu, l'accueil et l'administration.</summary>
    string DisplayName { get; }

    /// <summary>Classe d'icône Bootstrap (ex. <c>bi-briefcase</c>).</summary>
    string Icon { get; }

    /// <summary>Route racine du module (ex. <c>/travail</c>).</summary>
    string RootRoute { get; }

    /// <summary>Widget optionnel rendu sur l'accueil lorsque le module est actif.</summary>
    Type? HomeWidgetComponent => null;

    /// <summary>Enregistre les services DI du module (factory DbContext, services métier).</summary>
    void ConfigureServices(IServiceCollection services);
}
