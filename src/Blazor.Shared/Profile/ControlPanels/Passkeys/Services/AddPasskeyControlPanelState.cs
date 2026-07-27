using Core.Shared.Passkeys.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys.Services;

public class AddPasskeyControlPanelState : ControlPanelState
{
    public IReadOnlyList<PasskeyInfo> ExistingUserPasskeys { get; set; } = [];

    public string? Name { get; set; } = string.Empty;
}
