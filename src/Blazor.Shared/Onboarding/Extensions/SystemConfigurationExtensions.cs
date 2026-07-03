using System.Net;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Riok.Mapperly.Abstractions;

namespace Blazor.Shared.Onboarding.Extensions;

[Mapper(UseDeepCloning = true, IgnoreObsoleteMembersStrategy = IgnoreObsoleteMembersStrategy.Both)]
internal static partial class SystemConfigurationExtensions
{
    public static partial SystemConfiguration Clone(this SystemConfiguration source);

    private static IPAddress IPAddressToIPAddress(IPAddress ipAddress) => IPAddress.Parse(ipAddress.ToString());
}

internal static partial class SystemConfigurationExtensions
{
    public static void UpdateFrom(this SystemConfiguration target, INetworkInterfaceConfiguration source)
    {
        var networkInterfaces = target.NetworkInterfacesSettings.NetworkInterfaces;
        var networkInterfaceName = source.GetName();

        var networkInterface = networkInterfaces.FirstOrDefault(i => i.CommonInformation.Name == networkInterfaceName)
            ?? throw new ArgumentException($"{networkInterfaceName} not found in list of network interfaces ({string.Join(", ", networkInterfaces.Select(i => i.CommonInformation.Name))})");

        if (source.ConfigurationMode == IpConfigurationMode.AutomaticDhcp)
        {
            //  Only normalize the static IPv4 configuration when actually switching to DHCP. While the interface is
            //  already in DHCP mode, the address and gateway are managed by DHCP and reported back by the host, so they
            //  must be left untouched - otherwise an unchanged interface would appear modified when compared against the
            //  current (DHCP-assigned) configuration.

            if (!networkInterface.IPv4.DHCPEnabled)
            {
                networkInterface.IPv4.DHCPEnabled = true;

                networkInterface.IPv4.Gateway = null;
                networkInterface.IPv4.IPv4Details.Clear();
            }
        }
        else
        {
            networkInterface.IPv4.DHCPEnabled = false;

            networkInterface.IPv4.IPv4Details.Clear();
            networkInterface.IPv4.IPv4Details.Add(
                new IPv4Detail { IPAddress = IPAddress.Parse(source.IpAddress), Netmask = IPAddress.Parse(source.SubnetMask) });

            if (string.IsNullOrWhiteSpace(source.DefaultGateway))
                networkInterface.IPv4.Gateway = null;
            else
                networkInterface.IPv4.Gateway = IPAddress.Parse(source.DefaultGateway);
        }
    }

    public static void UpdateFrom(this SystemConfiguration target, IDnsConfiguration source)
    {
        var networkDnsSettings = target.NetworkDNSSettings;

        networkDnsSettings.NameServersEnabled = source.Enabled;

        networkDnsSettings.NameServers.Clear();
        networkDnsSettings.NameServers.AddRange(source.Details.Select(d => IPAddress.Parse(d.IpAddress)));
    }
}
