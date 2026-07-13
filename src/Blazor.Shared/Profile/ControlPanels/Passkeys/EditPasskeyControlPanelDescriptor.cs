using Blazor.Shared.Profile.Localization;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class EditPasskeyControlPanelDescriptor : IControlPanelDescriptor<EditPasskeyControlPanel>
{
    public string Title => EditPasskey.EditPasskeyTitle;
    public Uri? IconUrl => null;

    public bool ShowInNavigation => false;
}