using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys.Services;

public class AddPasskeyControlPanelResetHandler : IControlPanelResetHandler<AddPasskeyControlPanelState>
{
    public Task Reset(AddPasskeyControlPanelState state, CancellationToken cancellationToken)
    {
        state.Name = string.Empty;
        return Task.CompletedTask;
    }
}
