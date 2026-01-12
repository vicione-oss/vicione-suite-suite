using Blazor.Shared.Network.ControlPanels.Dns.Components;
using Blazor.Shared.Network.ControlPanels.Dns.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.Network.ControlPanels.Dns.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDnsControlPanel(this IServiceCollection services)
    {
        services.AddControlPanel<SharedClientModule, DnsControlPanel, DnsControlPanelState>()
            .WithSaveHandler<DnsControlPanelSaveHandler>()
            .WithResetHandler<DnsControlPanelResetHandler>();

        return services;
    }
}
