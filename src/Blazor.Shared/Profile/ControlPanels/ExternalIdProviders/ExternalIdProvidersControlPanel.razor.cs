using Blazor.Shared.Profile.ControlPanels.Passkeys.Components;
using Blazor.Shared.Services;
using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.Security;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.FeatureManagement;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Services;
using UnlinkError = Core.Shared.UserManagement.Events.ExternalLoginError;

namespace Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;

[ControlPanelCategory<ProfileCategoryDescriptor>]
[ControlPanelGroup<SecondaryControlPanelGroupDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId)]
public partial class ExternalIdProvidersControlPanel
{
    private bool _unlinkDialogVisible;
    private bool _isPasskeyFeatureEnabled;
    private SuiteUser? _currentUser;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    [Inject] private IExternalAuthenticationSettings ExternalAuthenticationSettings { get; set; } = default!;
    [Inject] private IExternalAccountService ExternalAccountService { get; set; } = default!;
    [Inject] private UserManager<SuiteUser> UserManager { get; set; } = default!;
    [Inject] private INavigationService NavigationService { get; set; } = default!;
    [Inject] private IFeatureManager FeatureManager { get; set; } = default!;
    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        State.IsExternalAuthenticationProviderConfigured =
            await ExternalAuthenticationSettings.IsExternalAuthenticationProviderConfigured();
        _isPasskeyFeatureEnabled = await FeatureManager.IsEnabledAsync(Core.Shared.Features.Constants.PasskeyFeatureName);

        var authenticationState = AuthState is null ? null : await AuthState;
        if (authenticationState is null)
            return;

        _currentUser = await UserManager.GetUserAsync(authenticationState.User);
        if (_currentUser is null)
            return;

        State.LinkedExternalAccount = await ExternalAccountService.GetExternalUserAccount(_currentUser);
    }

    private void OpenUnlinkConfirmation()
    {
        State.RemovalError = null;
        State.IsLastSignInMethod = false;
        _unlinkDialogVisible = true;
    }

    private async Task ConfirmUnlinkAccount()
    {
        if (_currentUser is null || State.LinkedExternalAccount is null)
            return;

        _unlinkDialogVisible = false;

        var result = await ExternalAccountService.RemoveExternalAccount(
            _currentUser,
            State.LinkedExternalAccount.LoginProvider,
            State.LinkedExternalAccount.ProviderKey);

        if (result is UserManagementServiceErrorResult errorResult)
        {
            State.RemovalError = errorResult.ErrorMessage;
            State.IsLastSignInMethod = errorResult.ErrorCode == (int)UnlinkError.LastCredential;
            return;
        }

        // TODO(#2786): replace "/" with a deep link to settings > profile > external-accounts.
        NavigationService.NavManager.NavigateTo(
            $"{IdentityRoutes.LoginRoute}?returnUrl={Uri.EscapeDataString("/")}",
            forceLoad: true);
    }

    private Task OpenPasskeys()
        => ControlPanelRequest.Send<PasskeysControlPanel>();
}
