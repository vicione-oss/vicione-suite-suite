using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

public sealed class UpdateResetControlPanelState : ControlPanelState
{
    internal bool ResetGroupExpanded { get; set; } = true;
}
