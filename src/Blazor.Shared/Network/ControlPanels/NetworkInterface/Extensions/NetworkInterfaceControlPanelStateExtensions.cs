using System.Globalization;
using System.Net;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts.Network;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;

internal static class NetworkInterfaceControlPanelStateExtensions
{
    extension(NetworkInterfaceControlPanelState state)
    {
        internal string GetVlanInformation(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is not null && networkInterface.IPv4.VLANEnabled)
                return networkInterface.IPv4.VLANID.ToString(CultureInfo.InvariantCulture);
            else
                return TechnicalTerms.Disabled;
        }

        internal List<IPAddress> GetDhcpDnsNameServers(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is not null && networkInterface.IPv4.DHCPEnabled)
                return state.DHCPLease?.NetworkDNSSettings?.NameServers ?? [];

            return [];
        }

        internal void UpdateIPv4Mode(NetworkInterfaceDetail networkInterface)
        {
            if (networkInterface.IPv4.DHCPEnabled)
            {
                state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;

                if (state.DHCPLease is not null && state.DHCPLease.IPv4Detail is not null)
                {
                    state.IpAddresses.Add(new(IpConfigurationMode.AutomaticDhcp,
                        state.DHCPLease.IPv4Detail.IPAddress,
                        state.DHCPLease.IPv4Detail.Netmask,
                        state.DHCPLease.Gateway));
                }
                else
                {
                    // This mode is called 'LinkLocal' by hostmanagement team - DHCP is enabld but no lease information is available,
                    // so we show an empty IP address with the gateway if available
                    state.IpAddresses.Add(new(IpConfigurationMode.AutomaticDhcp,
                        IPAddress.None,
                        IPAddress.None,
                        state.DHCPLease?.Gateway));
                }
            }
            else
            {
                state.IpV4ConfigurationMode = IpConfigurationMode.Manual;

                foreach (var detail in networkInterface.IPv4.IPv4Details)
                    state.IpAddresses.Add(new(IpConfigurationMode.Manual, detail.IPAddress, detail.Netmask, networkInterface.IPv4.Gateway));
            }

            state.DefaultGateway = networkInterface.IPv4.Gateway?.ToString();
            state.VLanEnabled = networkInterface.IPv4.VLANEnabled;
            state.VLanId = networkInterface.IPv4.VLANID.ToString(CultureInfo.InvariantCulture);
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

        internal void UpdateMacAddress(NetworkInterfaceDetail networkInterface, string? originalPhysicalAddress = null)
        {
            state.MacAddressManually = networkInterface.CommonInformation.PhysicalAddress != originalPhysicalAddress;

            if (state.MacAddressManually)
                state.MacAddress = networkInterface.CommonInformation.PhysicalAddress;
            else
                state.MacAddress = string.Empty;
        }

        internal void Reset()
        {
            state.Name = string.Empty;
            state.Enabled = false;
            state.DHCPLease = null;

            state.IpAddresses = [];
            state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;

            var ipV4Details = new List<NetworkInterfaceIPv4Detail>();
            ipV4Details.EnsureAtLeastOneItemExists();

            state.FirstIpV4Detail = ipV4Details.First();
            state.AdditionalIpV4Details = [.. ipV4Details.Skip(1)];

            state.DefaultGateway = null;

            state.MacAddressManually = false;
            state.MacAddress = string.Empty;

            state.VLanEnabled = false;
            state.VLanId = string.Empty;
        }
    }
}
