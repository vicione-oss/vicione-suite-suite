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

            PrimaryDNSSuffix = new() { Enabled = state.DnsSuffixEnabled, Suffix = state.DnsSuffix },

            MulticastDNSEnabled = state.MulticastDnsEnabled
        };

        // Name servers.
        {
            networkDnsSettings.NameServers.Enabled = state.DnsEnabled;

            // Unfilled fieldsets and duplicates are dropped.
            state.DnsDetails = [.. state.DnsDetails.Where(d => !string.IsNullOrWhiteSpace(d.IpAddress)).Distinct()];

            networkDnsSettings.NameServers.Addresses.Clear();
            networkDnsSettings.NameServers.Addresses.AddRange(state.DnsDetails.Select(d => IPAddress.Parse(d.IpAddress)));

            state.DnsDetails.EnsureAtLeastOneItemExists();
        }

        // Search domains.
        {
            networkDnsSettings.SearchDomains.Enabled = state.SearchDomainsEnabled;

            // Unfilled fieldsets and duplicates are dropped.
            state.SearchDomainDetails = [.. state.SearchDomainDetails.Where(d => !string.IsNullOrWhiteSpace(d.IpAddress)).Distinct()];

            networkDnsSettings.SearchDomains.Domains.Clear();
            networkDnsSettings.SearchDomains.Domains.AddRange(state.SearchDomainDetails.Select(d => d.IpAddress));

            state.SearchDomainDetails.EnsureAtLeastOneItemExists();
        }

        // Static hosts.
        {
            networkDnsSettings.StaticHosts.Enabled = state.StaticHostsEnabled;

            // Unfilled fieldsets and duplicates are dropped.
            state.StaticHostDetails = [.. state.StaticHostDetails
                .Where(d => !string.IsNullOrWhiteSpace(d.IpAddress) || !string.IsNullOrWhiteSpace(d.Hostname))
                .Distinct()];

            networkDnsSettings.StaticHosts.Hosts.Clear();
            networkDnsSettings.StaticHosts.Hosts.AddRange(state.StaticHostDetails
                .Select(d => new HostManagement.Shared.Contracts.Network.StaticHostDetail { IPAddress = IPAddress.Parse(d.IpAddress), Hostname = d.Hostname }));

            state.StaticHostDetails.EnsureAtLeastOneItemExists();
        }

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfaces = SystemConfigurationService.SystemConfiguration.NetworkInterfaces,
            NetworkDNSSettings = networkDnsSettings,
            NetworkProxySettings = SystemConfigurationService.SystemConfiguration.NetworkProxySettings,
            NetworkNTPSettings = SystemConfigurationService.SystemConfiguration.NetworkNTPSettings,
            Services = SystemConfigurationService.SystemConfiguration.Services
        };

        return Task.FromResult<ISaveInternalResult>(new SystemConfigurationSaveInternalResult(systemConfiguration));
    }
}
