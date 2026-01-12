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
            networkInterface.IPv4.DHCPEnabled = true;

            networkInterface.IPv4.Gateway = null;

            target.NetworkDNSSettings.NameServersEnabled = false;
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

            var networkDnsSettings = target.NetworkDNSSettings;

            if (string.IsNullOrWhiteSpace(source.DnsServer))
            {
                networkDnsSettings.NameServersEnabled = false;
            }
            else
            {
                networkDnsSettings.NameServersEnabled = true;

                networkDnsSettings.NameServers.Clear();
                networkDnsSettings.NameServers.Add(IPAddress.Parse(source.DnsServer));
            }
        }
    }
}
