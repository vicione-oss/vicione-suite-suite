using Blazor.Shared.Network.ControlPanels.Proxies.Components;
using Blazor.Shared.Network.ControlPanels.Proxies.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddProxiesControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, ProxiesControlPanel, ProxiesControlPanelState>()
            .WithSaveHandler<ProxiesControlPanelSaveHandler>()
            .WithResetHandler<ProxiesControlPanelResetHandler>();

        return services;
    }
}
