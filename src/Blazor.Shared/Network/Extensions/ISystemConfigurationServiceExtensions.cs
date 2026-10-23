using System.Net;
using Blazor.Shared.Services;

namespace Blazor.Shared.Network.Extensions;

internal static class ISystemConfigurationServiceExtensions
{
    extension(ISystemConfigurationService systemConfigurationService)
    {
        internal List<IPAddress> GetStaticDnsNameServers()
        {
            var nameServers = systemConfigurationService.SystemConfiguration.NetworkDNSSettings.NameServers;

            return nameServers.Enabled ? nameServers.Addresses : [];
        }
    }
}
