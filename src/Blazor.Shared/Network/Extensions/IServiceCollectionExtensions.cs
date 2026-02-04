using Blazor.Shared.Network.ControlPanels.Dns.Extensions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Network.ControlPanels.Ntp.Extensions;
using Blazor.Shared.Network.ControlPanels.Proxies.Extensions;
using Blazor.Shared.Network.ControlPanels.RemoteAccess.Extensions;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Network.Services.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNetwork()
        {
            services.AddScoped<IUpdateControlPanelRegistryHandler, UpdateControlPanelRegistryHandler>();

            services.AddDnsControlPanel()
                .AddNetworkInterfaceControlPanel()
                .AddNtpControlPanel()
                .AddProxiesControlPanel()
                .AddRemoteAccessControlPanel();

            return services;
        }

        public IServiceCollection AddIpAddressValidator()
        {
            services.TryAddScoped<IIpAddressValidator, IpAddressValidator>();

            return services;
        }
    }
}
