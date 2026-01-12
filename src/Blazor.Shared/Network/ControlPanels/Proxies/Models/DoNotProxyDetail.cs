namespace Blazor.Shared.Network.ControlPanels.Proxies.Models;

internal sealed record DoNotProxyDetail
{
    public string HostnameOrIp { get; set; } = string.Empty;
}
