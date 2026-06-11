using Blazor.Shared.Profile.Localization;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public sealed class PasskeyControlPanelDescriptor : IControlPanelDescriptor<PasskeysControlPanel>
{
    public string Title => Passkey.PasskeyTermPlural;
    public Uri? IconUrl => null;
}
