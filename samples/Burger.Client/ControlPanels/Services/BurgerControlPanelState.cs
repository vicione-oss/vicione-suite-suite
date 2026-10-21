using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Burger.Client.ControlPanels.Services;

[NotifyChanged]
public sealed partial class BurgerControlPanelState : ControlPanelState
{
    public partial string? Description { get; set; }
}
