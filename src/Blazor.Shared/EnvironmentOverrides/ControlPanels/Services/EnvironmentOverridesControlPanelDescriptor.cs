using Blazor.Shared.EnvironmentOverrides.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;

internal sealed class
    EnvironmentOverridesControlPanelDescriptor : IControlPanelDescriptor<EnvironmentOverridesControlPanel>
{
    public string Title => Localization.EnvironmentOverridesControlPanel.PanelTitle;
    public Uri? IconUrl => null;
}
