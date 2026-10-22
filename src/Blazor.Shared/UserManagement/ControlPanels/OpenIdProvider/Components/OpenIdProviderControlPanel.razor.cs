using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;
using Core.Shared.Security;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;

namespace Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Components;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class OpenIdProviderControlPanel : ControlPanelBase<OpenIdProviderControlPanelState>
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private string RedirectUri => Navigation.ToAbsoluteUri(ExternalLoginPaths.Callback).AbsoluteUri;

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

    private Task<string> GetRedirectUri() => Task.FromResult(RedirectUri);

    private async Task RemoveStoredClientSecret()
    {
        State.RemoveStoredClientSecret = true;
        State.ClientSecret = string.Empty;

        await BeginEdit();
    }
}
