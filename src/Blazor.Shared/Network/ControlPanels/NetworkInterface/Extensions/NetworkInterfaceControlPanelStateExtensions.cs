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
    internal static List<IpAdressInformation> GetIpv4Addresses(this NetworkInterfaceControlPanelState state, ISystemConfigurationService systemConfigurationService)
    {
        var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
            .ElementAtOrDefault(state.NetworkInterfaceIndex);

        var result = new List<IpAdressInformation>();

        if (networkInterface is null)
            return result;

        if (networkInterface.IPv4.DHCPEnabled && networkInterface.IPv4.DHCPLease is not null && networkInterface.IPv4.DHCPLease.IPv4Detail is not null)
        {
            result.Add(new(IpConfigurationMode.AutomaticDhcp,
                networkInterface.IPv4.DHCPLease.IPv4Detail.IPAddress,
                networkInterface.IPv4.DHCPLease.IPv4Detail.Netmask,
                networkInterface.IPv4.DHCPLease.Gateway));

            return result;
        }

        foreach (var detail in networkInterface.IPv4.IPv4Details)
            result.Add(new(IpConfigurationMode.Manual, detail.IPAddress, detail.Netmask, networkInterface.IPv4.Gateway));

        return result;
    }

    internal static string GetVlanInformation(this NetworkInterfaceControlPanelState state, ISystemConfigurationService systemConfigurationService)
    {
        var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
            .ElementAtOrDefault(state.NetworkInterfaceIndex);

        if (networkInterface is not null && networkInterface.IPv4.VLANEnabled)
            return networkInterface.IPv4.VLANID.ToString(CultureInfo.InvariantCulture);
        else
            return TechnicalTerms.Disabled;
    }

    internal static List<IPAddress> GetDhcpDnsNameServers(this NetworkInterfaceControlPanelState state, ISystemConfigurationService systemConfigurationService)
    {
        var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
            .ElementAtOrDefault(state.NetworkInterfaceIndex);

        if (networkInterface is not null && networkInterface.IPv4.DHCPEnabled)
            return networkInterface.IPv4.DHCPLease?.NetworkDNSSettings?.NameServers ?? [];

        return [];
    }
}
