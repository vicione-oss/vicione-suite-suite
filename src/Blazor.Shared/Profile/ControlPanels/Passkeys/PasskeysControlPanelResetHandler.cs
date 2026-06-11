using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class PasskeysControlPanelResetHandler : IControlPanelResetHandler<PasskeysControlPanelState>
{
    public Task Reset(PasskeysControlPanelState state, CancellationToken cancellationToken)
    {
        state.PasskeysMarkedForDeletion.Clear();
        return Task.CompletedTask;
    }
}
