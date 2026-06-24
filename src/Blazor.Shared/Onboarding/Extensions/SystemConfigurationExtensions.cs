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
        }
        else
        {
            networkInterface.IPv4.DHCPEnabled = false;

            //  IPv4Details and NameServers are only cleared/rebuilt when their content actually differs from the proposed values.
            //  Since IPv4Settings is a sealed record whose synthesized equality compares List<T> by reference, unnecessary list
            //  mutations caused false inequality even when values matched. Can be removed once this is fixed in Hostmangement.Shared

            var proposedIp = IPAddress.Parse(source.IpAddress);
            var proposedNetmask = IPAddress.Parse(source.SubnetMask);
            var ipv4Details = networkInterface.IPv4.IPv4Details;

            if (ipv4Details.Count != 1
                || !ipv4Details[0].IPAddress.Equals(proposedIp)
                || !ipv4Details[0].Netmask.Equals(proposedNetmask))
            {
                ipv4Details.Clear();
                ipv4Details.Add(new IPv4Detail { IPAddress = proposedIp, Netmask = proposedNetmask });
            }

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

                var proposedDns = IPAddress.Parse(source.DnsServer);

                if (networkDnsSettings.NameServers.Count != 1
                    || !networkDnsSettings.NameServers[0].Equals(proposedDns))
                {
                    networkDnsSettings.NameServers.Clear();
                    networkDnsSettings.NameServers.Add(proposedDns);
                }
            }
        }
    }
}
