using System.Globalization;
using System.Net;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Extensions;
using HostManagement.Shared.Contracts.Network;
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

        internal string GetPhysicalAddress(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is null)
                return CommonVocabulary.Unknown;

            var userDefinedMACAddress = networkInterface.CommonInformation.UserDefinedMACAddress;

            if (userDefinedMACAddress.Enabled)
                return userDefinedMACAddress.Address.ToColonNotation();
            else
                return state.OriginalMacAddress ?? CommonVocabulary.Unknown;
        }

        internal List<IPAddress> GetDhcpDnsNameServers(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is not null && networkInterface.IPv4.DHCPEnabled)
                return state.DHCPLease?.NetworkDNSSettings?.NameServers.Addresses ?? [];

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
            state.OriginalMacAddress = null;

            state.VLanEnabled = false;
            state.VLanId = string.Empty;
        }
    }
}
