using Blazor.Shared.Connections.ControlPanels;
using Blazor.Shared.Connections.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.Connections.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddConnectionManagement(this IServiceCollection services)
        => services
            .AddConnectionControlPanel()
            .AddConnectionsControlPanel()
            .AddTagControlPanel();

    internal static IServiceCollection AddConnectionServices(this IServiceCollection services)
    {
        services.TryAddScoped<ISuiteConnectionService, SuiteConnectionService>();

        // Suite has to provide <see cref="Sdk.Client.Services.IConnectionService"/> used by modules
        services.TryAddScoped<IConnectionService>(s => s.GetRequiredService<ISuiteConnectionService>());

        return services;
    }

    internal static IServiceCollection AddConnectionControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, ConnectionControlPanel, ConnectionControlPanelState>()
            .WithAutoDiscovery<ConnectionControlPanelDescriptor>()
            .WithSaveHandler<ConnectionControlPanelSaveHandler>()
            .WithResetHandler<ConnectionControlPanelResetHandler>();

        services.AddConnectionServices();

        services.AddScoped<TestConnectionService>();

        return services;
    }

    internal static IServiceCollection AddConnectionsControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, ConnectionsControlPanel, ConnectionsControlPanelState>()
            .WithAutoDiscovery<ConnectionsControlPanelDescriptor>()
            .WithSaveHandler<ConnectionsControlPanelSaveHandler>()
            .WithResetHandler<ConnectionsControlPanelResetHandler>();

        services.AddConnectionServices();

        services.AddGridItemSelectColumn()
            .AddGridItemSelection<Guid>(typeof(ConnectionsControlPanelServiceKey))
            .AddGridItemSelection<Guid>(typeof(TagsControlPanelPageContentServiceKey));

        return services;
    }

    internal static IServiceCollection AddTagControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, TagControlPanel, TagControlPanelState>()
            .WithAutoDiscovery<TagControlPanelDescriptor>()
            .WithSaveHandler<TagControlPanelSaveHandler>()
            .WithResetHandler<TagControlPanelResetHandler>();

        services.AddConnectionServices();

        services.AddCheckBox();

        return services;
    }
}
