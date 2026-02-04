using System.Globalization;
using System.Net;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;

internal static class NetworkInterfaceControlPanelStateExtensions
{
    extension(NetworkInterfaceControlPanelState state)
    {
        internal List<IpAdressInformation> GetIpv4Addresses(ISystemConfigurationService systemConfigurationService)
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            var result = new List<IpAdressInformation>();

            if (networkInterface is null)
                return result;

            if (networkInterface.IPv4.DHCPEnabled)
            {
                if (state.DHCPLease is not null && state.DHCPLease.IPv4Detail is not null)
                {
                    result.Add(new(IpConfigurationMode.AutomaticDhcp, state.DHCPLease.IPv4Detail.IPAddress, state.DHCPLease.IPv4Detail.Netmask, state.DHCPLease.Gateway));
                    state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;
                }
                else
                {
                    result.Add(new(IpConfigurationMode.LinkLocal, IPAddress.None, IPAddress.None, state.DHCPLease?.Gateway));
                    state.IpV4ConfigurationMode = IpConfigurationMode.LinkLocal;
                }

                return result;
            }

            foreach (var detail in networkInterface.IPv4.IPv4Details)
                result.Add(new(IpConfigurationMode.Manual, detail.IPAddress, detail.Netmask, networkInterface.IPv4.Gateway));

            return result;
        }

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
    }
}
