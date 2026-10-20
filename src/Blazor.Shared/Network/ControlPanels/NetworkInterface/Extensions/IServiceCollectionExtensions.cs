using Blazor.Shared.Dialogs.Extensions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddNetworkInterfaceGeneralControlPanel(this IServiceCollection services)
    {
        services.AddDialogs();

        services.TryAddScoped<NetworkInterfaceSaver>();

        services.AddControlPanel<SharedClientModule, NetworkInterfaceGeneralControlPanel, NetworkInterfaceControlPanelState>()
            .WithSaveHandler<NetworkInterfaceGeneralControlPanelSaveHandler>()
            .WithResetHandler<NetworkInterfaceGeneralControlPanelResetHandler>();

        return services;
    }

    public static IServiceCollection AddNetworkInterfaceTcpIpControlPanel(this IServiceCollection services)
    {
        services.AddDialogs();

        services.TryAddScoped<NetworkInterfaceSaver>();

        services.AddControlPanel<SharedClientModule, NetworkInterfaceTcpIpControlPanel, NetworkInterfaceControlPanelState>()
            .WithSaveHandler<NetworkInterfaceTcpIpControlPanelSaveHandler>()
            .WithResetHandler<NetworkInterfaceTcpIpControlPanelResetHandler>();

        return services;
    }

    public static IServiceCollection AddNetworkInterfaceHardwareControlPanel(this IServiceCollection services)
    {
        services.AddDialogs();

        services.TryAddScoped<NetworkInterfaceSaver>();

        services.AddControlPanel<SharedClientModule, NetworkInterfaceHardwareControlPanel, NetworkInterfaceControlPanelState>()
            .WithSaveHandler<NetworkInterfaceHardwareControlPanelSaveHandler>()
            .WithResetHandler<NetworkInterfaceHardwareControlPanelResetHandler>();

        return services;
    }
}
