namespace Blazor.Shared.Network.ControlPanels.Dns.Models;

internal sealed record NetworkInterfaceStaticHostDetail
{
    public string IpAddress { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
}
