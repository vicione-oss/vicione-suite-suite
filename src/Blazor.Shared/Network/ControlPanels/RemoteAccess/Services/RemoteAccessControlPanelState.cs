using Blazor.Shared.Network.ControlPanels.RemoteAccess.Models;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;

public sealed class RemoteAccessControlPanelState : NetworkControlPanelStateBase
{
    internal Terminal Terminal { get; } = new();
    internal bool IsSecureShellInitial { get; set; }
    internal bool IsMoneoRcInitial { get; set; }
}
