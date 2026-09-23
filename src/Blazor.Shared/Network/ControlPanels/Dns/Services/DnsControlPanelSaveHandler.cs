using System.Net;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Network.Models;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.Dns.Services;

internal sealed class DnsControlPanelSaveHandler(IUiMediator mediator, ISystemConfigurationService systemConfigurationService, ILogger<DnsControlPanelSaveHandler> logger)
    : NetworkControlPanelSaveHandlerBase<DnsControlPanelState>(mediator, systemConfigurationService, logger)
{
    protected override Task<ISaveInternalResult> SaveInternal(DnsControlPanelState state)
    {
        var networkDnsSettings = new HostManagement.Shared.Contracts.Network.NetworkDNSSettings
        {
            Hostname = state.Hostname,

            DNSSuffixEnabled = state.DnsSuffixEnabled,
            DNSSuffix = state.DnsSuffix,

            MulticastDNSEnabled = state.MulticastDnsEnabled
        };

        // Name servers.
        {
            networkDnsSettings.NameServersEnabled = state.DnsEnabled;

            // Unfilled fieldsets and duplicates are dropped.
            state.DnsDetails = [.. state.DnsDetails.Where(d => !string.IsNullOrWhiteSpace(d.IpAddress)).Distinct()];

            networkDnsSettings.NameServers.Clear();
            networkDnsSettings.NameServers.AddRange(state.DnsDetails.Select(d => IPAddress.Parse(d.IpAddress)));

            state.DnsDetails.EnsureAtLeastOneItemExists();
        }

        // Search domains.
        {
            networkDnsSettings.SearchDomainsEnabled = state.SearchDomainsEnabled;

            // Unfilled fieldsets and duplicates are dropped.
            state.SearchDomainDetails = [.. state.SearchDomainDetails.Where(d => !string.IsNullOrWhiteSpace(d.IpAddress)).Distinct()];

            networkDnsSettings.SearchDomains.Clear();
            networkDnsSettings.SearchDomains.AddRange(state.SearchDomainDetails.Select(d => d.IpAddress));

            state.SearchDomainDetails.EnsureAtLeastOneItemExists();
        }

        // Static hosts.
        {
            networkDnsSettings.StaticHostsEnabled = state.StaticHostsEnabled;

            // Unfilled fieldsets and duplicates are dropped.
            state.StaticHostDetails = [.. state.StaticHostDetails
                .Where(d => !string.IsNullOrWhiteSpace(d.IpAddress) || !string.IsNullOrWhiteSpace(d.Hostname))
                .Distinct()];

            networkDnsSettings.StaticHosts.Clear();
            networkDnsSettings.StaticHosts.AddRange(state.StaticHostDetails
                .Select(d => new HostManagement.Shared.Contracts.Network.StaticHostDetail { IPAddress = IPAddress.Parse(d.IpAddress), Hostname = d.Hostname }));

            state.StaticHostDetails.EnsureAtLeastOneItemExists();
        }

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfacesSettings = SystemConfigurationService.SystemConfiguration.NetworkInterfacesSettings,
            NetworkDNSSettings = networkDnsSettings,
            NetworkProxySettings = SystemConfigurationService.SystemConfiguration.NetworkProxySettings,
            NetworkNTPSettings = SystemConfigurationService.SystemConfiguration.NetworkNTPSettings,
            Services = SystemConfigurationService.SystemConfiguration.Services
        };

        return Task.FromResult<ISaveInternalResult>(new SystemConfigurationSaveInternalResult(systemConfiguration));
    }
}
