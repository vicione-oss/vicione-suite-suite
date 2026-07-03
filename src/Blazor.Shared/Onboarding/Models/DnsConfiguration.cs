using Blazor.Shared.Network.ControlPanels.Dns.Models;

namespace Blazor.Shared.Onboarding.Models;

internal sealed class DnsConfiguration : IDnsConfiguration
{
    public bool Enabled { get; set; }
    public List<NetworkInterfaceDnsDetail> Details { get; set; } = [];
}
