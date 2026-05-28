using Burger.Client.ControlPanels.Services;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;

namespace Burger.Client.ControlPanels;

[ControlPanelCategory<IControlPanelNetworkCategoryDescriptor>]
public sealed partial class BurgerControlPanel : ControlPanelBase<BurgerControlPanelState>;
