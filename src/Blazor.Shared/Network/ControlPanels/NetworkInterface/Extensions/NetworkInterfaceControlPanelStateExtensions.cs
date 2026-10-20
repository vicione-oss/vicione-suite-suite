using System.Globalization;
using System.Net;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;

internal static class NetworkInterfaceControlPanelStateExtensions
{
    extension(NetworkInterfaceControlPanelState state)
    {
        internal string GetVlanInformation(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is not null && networkInterface.VLAN.Enabled)
                return networkInterface.VLAN.ID.ToString(CultureInfo.InvariantCulture);
            else
                return TechnicalTerms.Disabled;
        }

        internal string GetMacAddress(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is null)
                return CommonVocabulary.Unknown;

            var userDefinedMACAddress = networkInterface.CommonInformation.UserDefinedMACAddress;

            if (userDefinedMACAddress.Enabled)
                return userDefinedMACAddress.Address.ToColonNotation();
            else
                return state.OriginalPhysicalAddress ?? CommonVocabulary.Unknown;
        }

        internal List<IPAddress> GetStaticDnsNameServers(ISystemConfigurationService systemConfigurationService)
        {
            var nameServers = systemConfigurationService.SystemConfiguration.NetworkDNSSettings.NameServers;

            return nameServers.Enabled ? nameServers.Addresses : [];
        }

        internal List<IPAddress> GetDhcpDnsNameServers(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is not null && networkInterface.IPv4.DHCPEnabled)
                return state.DHCPLease?.NetworkDNSSettings?.NameServers.Addresses ?? [];

            return [];
        }

        internal List<IpAddressInformation> GetIpAddresses(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is null)
                return [];

            if (!networkInterface.IPv4.DHCPEnabled)
            {
                return [.. networkInterface.IPv4.IPv4Details
                    .Select(detail => new IpAddressInformation(IpConfigurationMode.Manual, detail.IPAddress, detail.Netmask, networkInterface.IPv4.Gateway))];
            }

            if (state.DHCPLease?.IPv4Detail is { } leaseDetail)
                return [new(IpConfigurationMode.AutomaticDhcp, leaseDetail.IPAddress, leaseDetail.Netmask, state.DHCPLease.Gateway)];

            // HostManagement calls this mode 'LinkLocal': DHCP is enabled without lease information, so only the gateway is known.
            return [new(IpConfigurationMode.AutomaticDhcp, IPAddress.None, IPAddress.None, state.DHCPLease?.Gateway)];
        }

        internal async Task UpdateDHCPLease(IUiMediator mediator, NetworkInterfaceDetail networkInterface, CancellationToken cancellationToken)
        {
            if (!networkInterface.IPv4.DHCPEnabled)
            {
                state.DHCPLease = null;
                return;
            }

            var response = await mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
                new GetDHCPLeaseInformation(networkInterface.CommonInformation.Name), cancellationToken);

            state.DHCPLease = response.DHCPLease;
        }

        /// <summary>
        /// Fetches the lease when DHCP is enabled and no leased address is known, e.g. after the interface was switched to
        /// DHCP, as no reset handler runs after a save. A running reset fetches the lease itself.
        /// </summary>
        internal async Task EnsureDHCPLease(IUiMediator mediator, ISystemConfigurationService systemConfigurationService,
            CancellationToken cancellationToken)
        {
            if (state.IsLoading || state.DHCPLease?.IPv4Detail is not null)
                return;

            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is null || !networkInterface.IPv4.DHCPEnabled)
                return;

            await state.UpdateDHCPLease(mediator, networkInterface, cancellationToken);
        }

        internal void UpdateIPv4Settings(NetworkInterfaceDetail networkInterface)
        {
            state.IpV4ConfigurationMode = networkInterface.IPv4.DHCPEnabled ? IpConfigurationMode.AutomaticDhcp : IpConfigurationMode.Manual;

            state.UpdateIPv4Details(networkInterface);

            state.DefaultGateway = networkInterface.IPv4.Gateway?.ToString();
            state.VLanEnabled = networkInterface.VLAN.Enabled;
            state.VLanId = networkInterface.VLAN.ID.ToString(CultureInfo.InvariantCulture);
        }

        internal void UpdateIPv4Details(NetworkInterfaceDetail networkInterface)
        {
            List<NetworkInterfaceIPv4Detail> ipV4Details = [.. networkInterface.IPv4.IPv4Details
                    .Select(d => new NetworkInterfaceIPv4Detail { IpAddress = d.IPAddress.ToString(), SubnetMask = d.Netmask.ToString() })
                    .Distinct()];

            ipV4Details.EnsureAtLeastOneItemExists();

            state.FirstIpV4Detail = ipV4Details.First();
            state.AdditionalIpV4Details = [.. ipV4Details.Skip(1)];
        }

        internal void UpdateMacAddress(NetworkInterfaceDetail networkInterface)
        {
            var userDefinedMACAddress = networkInterface.CommonInformation.UserDefinedMACAddress;

            state.MacAddressManually = userDefinedMACAddress.Enabled;
            state.MacAddress = userDefinedMACAddress.Enabled ? userDefinedMACAddress.Address.ToColonNotation() : string.Empty;
        }

        internal void ResetGeneral()
        {
            state.Name = string.Empty;
            state.DHCPLease = null;
            state.OriginalPhysicalAddress = null;

            state.Enabled = false;
        }

        internal void ResetTcpIp()
        {
            state.Name = string.Empty;
            state.DHCPLease = null;

            state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;

            var ipV4Details = new List<NetworkInterfaceIPv4Detail>();
            ipV4Details.EnsureAtLeastOneItemExists();

            state.FirstIpV4Detail = ipV4Details.First();
            state.AdditionalIpV4Details = [.. ipV4Details.Skip(1)];

            state.DefaultGateway = null;

            state.VLanEnabled = false;
            state.VLanId = string.Empty;
        }

        internal void ResetHardware()
        {
            state.Name = string.Empty;
            state.OriginalPhysicalAddress = null;

            state.MacAddressManually = false;
            state.MacAddress = string.Empty;
        }
    }
}
