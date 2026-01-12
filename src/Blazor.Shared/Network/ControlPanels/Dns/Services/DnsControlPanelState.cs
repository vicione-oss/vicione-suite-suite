using Blazor.Shared.Network.ControlPanels.Dns.Models;

namespace Blazor.Shared.Network.ControlPanels.Dns.Services;

public sealed class DnsControlPanelState : NetworkControlPanelStateBase
{
    internal string Hostname { get; set; } = string.Empty;
    internal bool DnsSuffixEnabled { get; set; }
    internal string DnsSuffix { get; set; } = string.Empty;
    internal bool MulticastDnsEnabled { get; set; }
    internal bool DnsEnabled { get; set; }
    internal bool SearchDomainsEnabled { get; set; }
    internal bool StaticHostsEnabled { get; set; }
    internal List<NetworkInterfaceDnsDetail> DnsDetails { get; set; } = [];
    internal List<NetworkInterfaceSearchDomainDetail> SearchDomainDetails { get; set; } = [];
    internal List<NetworkInterfaceStaticHostDetail> StaticHostDetails { get; set; } = [];
}
