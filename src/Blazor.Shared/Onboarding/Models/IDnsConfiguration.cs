using Blazor.Shared.Network.ControlPanels.Dns.Models;

namespace Blazor.Shared.Onboarding.Models;

public interface IDnsConfiguration
{
    bool Enabled { get; set; }
    internal List<NetworkInterfaceDnsDetail> Details { get; set; }
}
