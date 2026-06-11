using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class RenamePasskeyControlPanelState : ControlPanelState
{
    public string? PasskeyId { get; set; }

    public string? CurrentName { get; set; }

    public string? NewName { get; set; }

    public IReadOnlyList<string> ExistingNames { get; set; } = [];

    public string? UserId { get; set; }
}