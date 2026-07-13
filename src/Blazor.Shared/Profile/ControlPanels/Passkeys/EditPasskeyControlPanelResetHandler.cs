using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class EditPasskeyControlPanelResetHandler : IControlPanelResetHandler<EditPasskeyControlPanelState>
{
    public Task Reset(EditPasskeyControlPanelState state, CancellationToken cancellationToken)
    {
        state.NewName = state.CurrentName;
        return Task.CompletedTask;
    }
}