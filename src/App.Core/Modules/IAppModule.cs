namespace App.Core.Modules;

public interface IAppModule
{
    string Key { get; }

    string DisplayName { get; }

    string Icon { get; }

    string RootRoute { get; }

    Type? HomeWidgetComponent => null;

    void ConfigureServices(IServiceCollection services);
}
