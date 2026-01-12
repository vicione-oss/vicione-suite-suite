using Blazor.Shared.Enums;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserInterface.ControlPanels.Theme.Services;

public sealed class ThemeControlPanelState : ControlPanelState
{
    internal LoginDesign SelectedLoginDesign { get; set; }
}
