using System.Net;
using System.Net.Sockets;
using Core.Shared.HostManagement.Extensions;
using HostManagement.Shared.Contracts.Network;
using HostManagement.Shared.Contracts.Service;
using Sdk.SystemConfiguration.Contracts;
using HmContracts = HostManagement.Shared.Contracts;
using HmServiceState = HostManagement.Shared.Enums.ServiceState;
using SdkServiceState = Sdk.SystemConfiguration.Contracts.ServiceState;
using PhysicalAddress = System.Net.NetworkInformation.PhysicalAddress;

namespace Core.OS.HostManagement.Mappers;

public static class SystemConfigurationMapper
{
    public static SystemConfiguration ToSuiteFormat(
        this HmContracts.SystemConfiguration? source,
        SystemConfigurationAdditionalData additionalData)
    {
        if (source is null)
            return new SystemConfiguration();

        return new SystemConfiguration
        {
            NetworkInterfaces = MapNetworkInterfaces(source.NetworkInterfaces, additionalData),
            Dns = MapDnsSettings(source.NetworkDNSSettings),
            Proxy = MapProxySettings(source.NetworkProxySettings),
            Ntp = MapNtpSettings(source.NetworkNTPSettings, additionalData.NtpFallbackServers),
            Services = MapServices(source.Services),
        };
    }

    private static List<NetworkInterface> MapNetworkInterfaces(List<NetworkInterfaceDetail>? networkInterfaces, SystemConfigurationAdditionalData additionalData)
        => networkInterfaces is null ? [] : networkInterfaces.Select(d => MapNetworkInterface(d, additionalData)).ToList();

    private static NetworkInterface MapNetworkInterface(NetworkInterfaceDetail detail, SystemConfigurationAdditionalData additionalData)
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
            additionalData.DhcpLeases.TryGetValue(common.Name, out var lease);

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

        VlanInfo? vlan = detail.VLAN.Enabled ? new VlanInfo { Id = detail.VLAN.ID } : null;

        return new NetworkInterface
        {
            Name = common.Name,
            PhysicalAddress = MapPhysicalAddress(common, additionalData.OriginalPhysicalAddresses),
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

    private static string MapPhysicalAddress(NetworkInterfaceCommonInformation common, Dictionary<string, PhysicalAddress?> originalPhysicalAddresses)
    {
        if (common.UserDefinedMACAddress.Enabled)
            return common.UserDefinedMACAddress.Address.ToColonNotation();

        originalPhysicalAddresses.TryGetValue(common.Name, out var originalPhysicalAddress);

        return originalPhysicalAddress?.ToColonNotation() ?? string.Empty;
    }

    private static DnsSettings MapDnsSettings(NetworkDNSSettings? source)
    {
        if (source is null)
            return new DnsSettings();

        return new DnsSettings
        {
            Hostname = source.Hostname,
            MulticastDnsEnabled = source.MulticastDNSEnabled,
            NameServers = source.NameServers.Enabled ? source.NameServers.Addresses.ToList() : [],
            DnsSuffix = source.PrimaryDNSSuffix.Enabled ? source.PrimaryDNSSuffix.Suffix : string.Empty,
            SearchDomains = source.SearchDomains.Enabled ? source.SearchDomains.Domains.ToList() : [],
            StaticHosts = source.StaticHosts.Enabled
                ? source.StaticHosts.Hosts.Select(h => new StaticHost
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
            DoNotProxyList = source.NoProxy.Enabled ? source.NoProxy.Entries.ToList() : [],
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
            Servers = source.Enabled ? source.Servers.ToList() : [],
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
