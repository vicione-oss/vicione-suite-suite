using Blazor.Shared.Profile.ControlPanels.Passkeys.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys.Services;

public sealed class PasskeysControlPanelDescriptor : IControlPanelDescriptor<PasskeysControlPanel>
{
    public string Title => Components.Localization.PasskeysControlPanel.PasskeyTermPlural;
    public Uri? IconUrl => null;
}
