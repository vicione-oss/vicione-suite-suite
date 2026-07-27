using Blazor.Shared.Profile.ControlPanels.Passkeys.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys.Services;

public class EditPasskeyControlPanelDescriptor : IControlPanelDescriptor<EditPasskeyControlPanel>
{
    public string Title => Components.Localization.EditPasskeyControlPanel.EditPasskeyTitle;
    public Uri? IconUrl => null;

    public bool ShowInNavigation => false;
}
