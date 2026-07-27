using Blazor.Shared.Profile.ControlPanels.Passkeys.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys.Services;

public class AddPasskeyControlPanelDescriptor : IControlPanelDescriptor<AddPasskeyControlPanel>
{
    public string Title => Components.Localization.AddPasskeyControlPanel.AddPasskeyTitle;
    public Uri? IconUrl => null;

    public bool ShowInNavigation => false;
}
