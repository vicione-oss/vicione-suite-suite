using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;

namespace Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class OpenIdProviderControlPanel : ControlPanelBase<OpenIdProviderControlPanelState>
{
    private bool RemoveStoredClientSecretAvailable
        => State.ClientSecretStored && !State.RemoveStoredClientSecret && State.ClientSecret.Length == 0;

    private string ClientSecretSubline
    {
        get
        {
            if (State.RemoveStoredClientSecret && State.ClientSecret.Length == 0)
                return Localization.OpenIdProviderControlPanel.ClientSecretRemovalPendingHint;

            return State.ClientSecretStored
                ? Localization.OpenIdProviderControlPanel.ClientSecretStoredHint
                : Localization.OpenIdProviderControlPanel.ClientSecretNotStoredHint;
        }
    }

    private async Task RemoveStoredClientSecret()
    {
        State.RemoveStoredClientSecret = true;
        State.ClientSecret = string.Empty;

        await BeginEdit();
    }
}
