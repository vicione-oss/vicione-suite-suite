using Blazor.Shared.Dialogs.Extensions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddNetworkInterfaceControlPanel(this IServiceCollection services)
    {
        services.AddDialogs();

        services.AddControlPanel<SharedClientModule, NetworkInterfaceControlPanel, NetworkInterfaceControlPanelState>()
            .WithSaveHandler<NetworkInterfaceControlPanelSaveHandler>()
            .WithResetHandler<NetworkInterfaceControlPanelResetHandler>();

        return services;
    }
}
