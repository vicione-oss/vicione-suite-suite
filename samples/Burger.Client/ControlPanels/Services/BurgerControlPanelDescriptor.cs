using Burger.Client.Localization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace Burger.Client.ControlPanels.Services;

internal sealed class BurgerControlPanelDescriptor : IControlPanelDescriptor<BurgerControlPanel>
{
    public string Title => Common.ModuleTitle;
    public Uri? IconUrl => ModuleAssetHelper.GetModuleIconUrl<BurgerClientModule>("burger.svg");
    public bool ShowInNavigation => true;
}
