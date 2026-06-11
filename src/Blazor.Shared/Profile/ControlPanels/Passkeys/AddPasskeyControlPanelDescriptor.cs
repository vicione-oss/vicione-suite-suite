using Blazor.Shared.Profile.Localization;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class AddPasskeyControlPanelDescriptor : IControlPanelDescriptor<AddPasskeyControlPanel>
{
    public string Title => AddPasskey.AddPasskeyTitle;
    public Uri? IconUrl => null;

    public bool ShowInNavigation => false;
}
