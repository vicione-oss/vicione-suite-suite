using Blazor.Shared.UserInterface.ControlPanels.Theme.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserInterface.ControlPanels.Theme.Services;

internal sealed class ThemeControlPanelDescriptor : IControlPanelDescriptor<ThemeControlPanel>
{
    public string Title => TechnicalTerms.Theme;
    public Uri? IconUrl => null;
}
