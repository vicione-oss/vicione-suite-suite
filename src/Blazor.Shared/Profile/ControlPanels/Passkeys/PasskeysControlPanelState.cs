using Core.Shared.UserManagement.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public sealed class PasskeysControlPanelState : ControlPanelState
{
    public List<string> PasskeysMarkedForDeletion { get; } = [];

    public SuiteUser? User { get; set; }

    public void ClearMarkedPasskeys()
    {
        PasskeysMarkedForDeletion.Clear();

        OnPropertyChanged(nameof(PasskeysMarkedForDeletion));
    }
}
