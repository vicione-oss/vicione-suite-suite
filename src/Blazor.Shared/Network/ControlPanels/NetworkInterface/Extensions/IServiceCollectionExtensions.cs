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

        services.AddScoped<NetworkInterfaceSaver>();

        services.AddControlPanel<SharedClientModule, NetworkInterfaceGeneralControlPanel, NetworkInterfaceControlPanelState>()
            .WithSaveHandler<NetworkInterfaceGeneralControlPanelSaveHandler>()
            .WithResetHandler<NetworkInterfaceGeneralControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, NetworkInterfaceTcpIpControlPanel, NetworkInterfaceControlPanelState>()
            .WithSaveHandler<NetworkInterfaceTcpIpControlPanelSaveHandler>()
            .WithResetHandler<NetworkInterfaceTcpIpControlPanelResetHandler>();

        services.AddControlPanel<SharedClientModule, NetworkInterfaceHardwareControlPanel, NetworkInterfaceControlPanelState>()
            .WithSaveHandler<NetworkInterfaceHardwareControlPanelSaveHandler>()
            .WithResetHandler<NetworkInterfaceHardwareControlPanelResetHandler>();

        return services;
    }
}
