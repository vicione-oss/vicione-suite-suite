using Blazor.Shared.Network.ControlPanels.Proxies.Models;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Services;

public sealed class ProxiesControlPanelState : NetworkControlPanelStateBase
{
    internal ProxySettings HttpProxySettings { get; set; } = new();
    internal ProxySettings HttpsProxySettings { get; set; } = new();
    internal ProxySettings SocksProxySettings { get; set; } = new();
    internal ProxySettings FtpProxySettings { get; set; } = new();
    internal ProxySettings SftpProxySettings { get; set; } = new();

    internal bool DoNotProxyListEnabled { get; set; }
    internal List<DoNotProxyDetail> DoNotProxyDetails { get; set; } = [];
}
