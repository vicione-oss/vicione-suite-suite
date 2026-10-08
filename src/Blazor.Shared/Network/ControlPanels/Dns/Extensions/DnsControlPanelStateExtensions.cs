using Blazor.Shared.Network.ControlPanels.Dns.Models;
using Blazor.Shared.Network.ControlPanels.Dns.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Services;

namespace Blazor.Shared.Network.ControlPanels.Dns.Extensions;

internal static class DnsControlPanelStateExtensions
{
    public static void Initialize(this DnsControlPanelState state, ISystemConfigurationService systemConfigurationService)
    {
        var networkDnsSettings = systemConfigurationService.SystemConfiguration.NetworkDNSSettings;

        state.Hostname = networkDnsSettings.Hostname;
        state.DnsSuffixEnabled = networkDnsSettings.PrimaryDNSSuffix.Enabled;
        state.DnsSuffix = networkDnsSettings.PrimaryDNSSuffix.Suffix;

        state.MulticastDnsEnabled = networkDnsSettings.MulticastDNSEnabled;

        state.DnsEnabled = networkDnsSettings.NameServers.Enabled;

        state.DnsDetails = [.. networkDnsSettings.NameServers.Addresses
            .Select(d => new NetworkInterfaceDnsDetail { IpAddress = d.ToString() })
            .Distinct()];

        state.DnsDetails.EnsureAtLeastOneItemExists();

        state.SearchDomainsEnabled = networkDnsSettings.SearchDomains.Enabled;

        state.SearchDomainDetails = [.. networkDnsSettings.SearchDomains.Domains
            .Select(d => new NetworkInterfaceSearchDomainDetail { IpAddress = d })
            .Distinct()];

        state.SearchDomainDetails.EnsureAtLeastOneItemExists();

        state.StaticHostsEnabled = networkDnsSettings.StaticHosts.Enabled;

        state.StaticHostDetails = [.. networkDnsSettings.StaticHosts.Hosts
            .Select(d => new NetworkInterfaceStaticHostDetail { IpAddress = d.IPAddress.ToString(), Hostname = d.Hostname })
            .Distinct()];

        state.StaticHostDetails.EnsureAtLeastOneItemExists();
    }
}
