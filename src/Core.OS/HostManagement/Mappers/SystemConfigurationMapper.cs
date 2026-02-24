using System.Net;
using System.Net.Sockets;
using HostManagement.Shared.Contracts.Network;
using HostManagement.Shared.Contracts.Service;
using Sdk.SystemConfiguration.Contracts;
using HmContracts = HostManagement.Shared.Contracts;
using HmServiceState = HostManagement.Shared.Enums.ServiceState;
using SdkServiceState = Sdk.SystemConfiguration.Contracts.ServiceState;

namespace Core.OS.HostManagement.Mappers;

public static class SystemConfigurationMapper
{
    public static SystemConfiguration ToSuiteFormat(
        this HmContracts.SystemConfiguration? source,
        Dictionary<string, DHCPLease?> dhcpLeases,
        List<string> ntpFallbackServers)
    {
        if (source is null)
            return new SystemConfiguration();

        return new SystemConfiguration
        {
            Version = source.Version,
            NetworkInterfaces = MapNetworkInterfaces(source.NetworkInterfacesSettings, dhcpLeases),
            Dns = MapDnsSettings(source.NetworkDNSSettings),
            Proxy = MapProxySettings(source.NetworkProxySettings),
            Ntp = MapNtpSettings(source.NetworkNTPSettings, ntpFallbackServers),
            Services = MapServices(source.Services),
        };
    }

    private static List<NetworkInterface> MapNetworkInterfaces(NetworkInterfacesSettings? settings, Dictionary<string, DHCPLease?> dhcpLeases)
        => settings is null ? [] : settings.NetworkInterfaces.Select(d => MapNetworkInterface(d, dhcpLeases)).ToList();

    private static NetworkInterface MapNetworkInterface(NetworkInterfaceDetail detail, Dictionary<string, DHCPLease?> dhcpLeases)
    {
        var ipv4 = detail.IPv4;
        var common = detail.CommonInformation;

        // Resolve effective IP address, netmask and gateway
        IPAddress? effectiveIp = null;
        IPAddress? effectiveNetmask = null;
        IPAddress? effectiveGateway = ipv4.Gateway;
        DhcpLeaseInfo? dhcpLease = null;
        List<IPv4Detail> additionalDetails;

        if (ipv4.DHCPEnabled)
        {
            dhcpLeases.TryGetValue(common.Name, out var lease);

            if (lease?.IPv4Detail is not null)
            {
                effectiveIp = lease.IPv4Detail.IPAddress;
                effectiveNetmask = lease.IPv4Detail.Netmask;
                effectiveGateway = lease.Gateway ?? ipv4.Gateway;

                dhcpLease = new DhcpLeaseInfo
                {
                    LeaseObtained = lease.LeaseObtained,
                    LeaseExpires = lease.LeaseExpires,
                };
            }

            // All configured static addresses are additional when DHCP is active
            additionalDetails = [.. ipv4.IPv4Details];
        }
        else
        {
            // First static address is primary, rest are additional
            if (ipv4.IPv4Details.Count > 0)
            {
                effectiveIp = ipv4.IPv4Details[0].IPAddress;
                effectiveNetmask = ipv4.IPv4Details[0].Netmask;
                additionalDetails = ipv4.IPv4Details.Skip(1).ToList();
            }
            else
            {
                additionalDetails = [];
            }
        }

        VlanInfo? vlan = ipv4.VLANEnabled ? new VlanInfo { Id = ipv4.VLANID } : null;

        return new NetworkInterface
        {
            Name = common.Name,
            PhysicalAddress = common.PhysicalAddress,
            Enabled = common.Enabled,
            IPv4Address = effectiveIp,
            IPv4Netmask = effectiveNetmask,
            IPv4Gateway = effectiveGateway,
            DhcpLease = dhcpLease,
            Vlan = vlan,
            AdditionalAddresses = additionalDetails.Select(d => new IpAddressInfo
            {
                AddressFamily = AddressFamily.InterNetwork,
                IpAddress = d.IPAddress,
                Netmask = d.Netmask,
            }).ToList(),
        };
    }

    private static DnsSettings MapDnsSettings(NetworkDNSSettings? source)
    {
        if (source is null)
            return new DnsSettings();

        return new DnsSettings
        {
            Hostname = source.Hostname,
            MulticastDnsEnabled = source.MulticastDNSEnabled,
            NameServers = source.NameServersEnabled ? source.NameServers.ToList() : [],
            DnsSuffix = source.DNSSuffixEnabled ? source.DNSSuffix : string.Empty,
            SearchDomains = source.SearchDomainsEnabled ? source.SearchDomains.ToList() : [],
            StaticHosts = source.StaticHostsEnabled
                ? source.StaticHosts.Select(h => new StaticHost
                {
                    IpAddress = h.IPAddress,
                    Hostname = h.Hostname,
                }).ToList()
                : [],
        };
    }

    private static ProxySettings MapProxySettings(NetworkProxySettings? source)
    {
        if (source is null)
            return new ProxySettings();

        return new ProxySettings
        {
            Http = MapProxyDetail(source.HTTP),
            Https = MapProxyDetail(source.HTTPS),
            Ftp = MapProxyDetail(source.FTP),
            Sftp = MapProxyDetail(source.SFTP),
            Socks = MapProxyDetail(source.SOCKS),
            DoNotProxyList = source.DoNotProxyListEnabled ? source.DoNotProxyList.ToList() : [],
        };
    }

    private static ProxyInfo? MapProxyDetail(NetworkProxyDetail? detail)
    {
        if (detail is null || !detail.Enabled)
            return null;

        return new ProxyInfo
        {
            Server = detail.Server ?? string.Empty,
            Port = detail.Port ?? 0,
            Username = detail.Username,
            Password = detail.Password,
        };
    }

    private static NtpSettings MapNtpSettings(NetworkNTPSettings? source, List<string> fallbackServers)
    {
        if (source is null)
            return new NtpSettings();

        return new NtpSettings
        {
            Servers = source.NTPServersEnabled ? source.NTPServers.ToList() : [],
            FallbackServers = fallbackServers,
        };
    }


    private static List<ServiceInfo> MapServices(List<ServiceDetail>? services)
        => services is null
            ? []
            : services.Select(s => new ServiceInfo
                {
                    Name = s.Name,
                    State = MapServiceState(s.State),
                })
                .ToList();

    private static SdkServiceState MapServiceState(HmServiceState state)
        => state switch
        {
            HmServiceState.Enabled => SdkServiceState.Enabled,
            HmServiceState.Disabled => SdkServiceState.Disabled,
            _ => SdkServiceState.Unknown,
        };
}
