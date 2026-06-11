using Core.Shared.Passkeys.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class AddPasskeysControlPanelState : ControlPanelState
{
    public IReadOnlyList<PasskeyInfo> ExistingUserPasskeys { get; set; } = [];

    public string? Name { get; set; } = string.Empty;
}
