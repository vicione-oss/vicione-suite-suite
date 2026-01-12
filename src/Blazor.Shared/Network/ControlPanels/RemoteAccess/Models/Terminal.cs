namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Models;

internal sealed class Terminal
{
    public bool CanSecureShell { get; set; }
    public bool IsSecureShell { get; set; }

    public bool CanMoneoRc { get; set; }
    public bool IsMoneoRc { get; set; }
}
