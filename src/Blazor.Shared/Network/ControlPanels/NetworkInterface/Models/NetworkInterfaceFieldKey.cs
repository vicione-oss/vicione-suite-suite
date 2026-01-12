namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;

internal sealed record NetworkInterfaceFieldKey
{
    public required int DetailIndex { get; init; }
    public required int FieldIndex { get; init; }
}
