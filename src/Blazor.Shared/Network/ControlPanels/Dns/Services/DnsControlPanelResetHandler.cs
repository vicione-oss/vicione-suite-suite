using Blazor.Shared.Network.ControlPanels.Dns.Models;
using Blazor.Shared.Network.Extensions;
using Core.Shared.HostManagement.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.Dns.Services;

internal sealed class DnsControlPanelResetHandler(ISystemConfigurationService systemConfigurationService) : IControlPanelResetHandler<DnsControlPanelState>
{
    public Task Reset(DnsControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            var networkDnsSettings = systemConfigurationService.SystemConfiguration.NetworkDNSSettings;

            state.Hostname = networkDnsSettings.Hostname;
            state.DnsSuffixEnabled = networkDnsSettings.DNSSuffixEnabled;
            state.DnsSuffix = networkDnsSettings.DNSSuffix;

            state.MulticastDnsEnabled = networkDnsSettings.MulticastDNSEnabled;

            state.DnsEnabled = networkDnsSettings.NameServersEnabled;

            state.DnsDetails = [.. networkDnsSettings.NameServers
            .Select(d => new NetworkInterfaceDnsDetail { IpAddress = d.ToString() })
            .Distinct()];

            state.DnsDetails.EnsureAtLeastOneItemExists();

            state.SearchDomainsEnabled = networkDnsSettings.SearchDomainsEnabled;

            state.SearchDomainDetails = [.. networkDnsSettings.SearchDomains
            .Select(d => new NetworkInterfaceSearchDomainDetail { IpAddress = d })
            .Distinct()];

            state.SearchDomainDetails.EnsureAtLeastOneItemExists();

            state.StaticHostsEnabled = networkDnsSettings.StaticHostsEnabled;

            state.StaticHostDetails = [.. networkDnsSettings.StaticHosts
            .Select(d => new NetworkInterfaceStaticHostDetail { IpAddress = d.IPAddress.ToString(), Hostname = d.Hostname })
            .Distinct()];

            state.StaticHostDetails.EnsureAtLeastOneItemExists();
        }
        finally
        {
            state.EndLoading();
        }

        return Task.CompletedTask;
    }
}
