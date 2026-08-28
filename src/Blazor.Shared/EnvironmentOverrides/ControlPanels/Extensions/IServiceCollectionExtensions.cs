using Blazor.Shared.Dialogs.Extensions;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Components;
using Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Blazor.Components.Grid.Extensions;

namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddEnvironmentOverridesControlPanel(this IServiceCollection services)
    {
        services.AddDialogs();

        services.AddScoped<IUpdateControlPanelRegistryHandler, EnvironmentOverridesControlPanelGate>();

        services.AddControlPanel<SharedClientModule, EnvironmentOverridesControlPanel, EnvironmentOverridesControlPanelState>()
            .WithAutoDiscovery<EnvironmentOverridesControlPanelDescriptor>()
            .WithSaveHandler<EnvironmentOverridesControlPanelSaveHandler>()
            .WithResetHandler<EnvironmentOverridesControlPanelResetHandler>();

        services.AddGridItemSelectColumn()
                .AddGridItemSelection<Guid>(typeof(EnvironmentOverridesControlPanelServiceKey));

        return services;
    }
}
