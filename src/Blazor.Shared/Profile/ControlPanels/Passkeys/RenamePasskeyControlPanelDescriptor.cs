using Blazor.Shared.Profile.Localization;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class RenamePasskeyControlPanelDescriptor : IControlPanelDescriptor<RenamePasskeyControlPanel>
{
    public string Title => RenamePasskey.RenamePasskeyTitle;
    public Uri? IconUrl => null;

    public bool ShowInNavigation => false;
}