using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class RenamePasskeyControlPanelResetHandler : IControlPanelResetHandler<RenamePasskeyControlPanelState>
{
    public Task Reset(RenamePasskeyControlPanelState state, CancellationToken cancellationToken)
    {
        state.NewName = state.CurrentName;
        return Task.CompletedTask;
    }
}