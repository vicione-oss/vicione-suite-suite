using Blazor.Shared.Network.ControlPanels.Ntp.Models;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Services;

public sealed class NtpControlPanelState : NetworkControlPanelStateBase
{
    internal bool NtpServersEnabled { get; set; }
    internal List<NtpServerDetail> NtpServerDetails { get; } = [];
    internal List<NtpServerDetail> FallbackNtpServerDetails { get; } = [];
}
