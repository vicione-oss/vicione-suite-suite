namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;

internal sealed record NetworkInterfaceIPv4Detail
{
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
}
