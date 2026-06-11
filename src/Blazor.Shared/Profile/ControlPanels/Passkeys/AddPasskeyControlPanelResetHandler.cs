using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class AddPasskeyControlPanelResetHandler : IControlPanelResetHandler<AddPasskeysControlPanelState>
{
    public Task Reset(AddPasskeysControlPanelState state, CancellationToken cancellationToken)
    {
        state.Name = string.Empty;
        return Task.CompletedTask;
    }
}
