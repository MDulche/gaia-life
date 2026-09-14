using Microsoft.Extensions.DependencyInjection;

namespace App.Shared.Modules;

public interface IAppModule
{
    string Key { get; }

    string DisplayName { get; }

    string Icon { get; }

    string RootRoute { get; }

    Type? HomeWidgetComponent => null;

    void ConfigureServices(IServiceCollection services);
}
