using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;

namespace Blazor.Shared.Tests.Onboarding;

internal static class SystemConfigurations
{
    /// <summary>
    /// Mirrors a factory-fresh device: lan1 (internet connection) and lan2 (local network) both on DHCP.
    /// </summary>
    public static SystemConfiguration CreateDhcp(string hostname = "edge") => new()
    {
        NetworkInterfaces =
        [
            new NetworkInterfaceDetail
            {
                CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan1", Enabled = true },
                IPv4 = new IPv4Settings { DHCPEnabled = true }
            },
            new NetworkInterfaceDetail
            {
                CommonInformation = new NetworkInterfaceCommonInformation { Name = "lan2", Enabled = true },
                IPv4 = new IPv4Settings { DHCPEnabled = true }
            }
        ],
        NetworkDNSSettings = new NetworkDNSSettings { Hostname = hostname, NameServers = new() { Enabled = false } },
        NetworkProxySettings = new NetworkProxySettings(),
        NetworkNTPSettings = new NetworkNTPSettings()
    };
}
