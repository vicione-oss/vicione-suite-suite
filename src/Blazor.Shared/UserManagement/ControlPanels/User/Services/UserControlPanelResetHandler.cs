using Blazor.Shared.UserManagement.ControlPanels.User.Extensions;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Extensions;
using Core.Shared.UserManagement.Contracts;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

internal sealed class UserControlPanelResetHandler(IUserService userService, IModuleAuthorizationClaimParser claimParser, IClaimsProvider claimsProvider) : IControlPanelResetHandler<UserControlPanelState>
{
    public async Task Reset(UserControlPanelState state, CancellationToken cancellationToken)
    {
        await UpdateUserProfile(state);

        state.CurrentPassword = null;
        state.NewPassword = null;
        state.RepeatNewPassword = null;
        state.PasswordExpirationDateString = null;

        await UpdatePermissionGridItems(state);

        ResetSelectedCulture(state);
        ResetSelectedTimeZone(state);

        state.UpdateAvailableUserRoles();

        state.PermissionEditContext = null;
        state.RoleToAdd = new(state.AvailableUserRoles?.FirstOrDefault() ?? string.Empty);
    }

    private async Task UpdatePermissionGridItems(UserControlPanelState state)
    {
        await state.UpdateAvailableClaims(claimParser, claimsProvider);
        state.UpdateGridItems(claimParser);
    }

    private async Task UpdateUserProfile(UserControlPanelState state)
    {
        if (state.UserName is not null)
        {
            var userProfile = await GetUserProfileAsync(state);

            if (userProfile != null)
                state.UserProfile = userProfile;
            else
                throw new InvalidOperationException(Localization.UserControlPanelResetHandler.FailedToFetchUserProfile);
        }
        else
        {
            state.UserProfile = new UserProfile() { UserName = UserName.Empty, };
        }
    }

    private async Task<UserProfile?> GetUserProfileAsync(UserControlPanelState state)
    {
        state.BeginLoading();
        try
        {
            var users = await userService.GetUsers(state.UserName);

            return users.FirstOrDefault();
        }
        finally
        {
            state.EndLoading();
        }
    }

    private static void ResetSelectedCulture(UserControlPanelState state)
        => state.SelectedCulture = CrossInstanceConfiguration.FindSupportedCulture(state.UserProfile?.Language);

    private static void ResetSelectedTimeZone(UserControlPanelState state)
    {
        if (state.UserProfile?.TimeZone is null
            || !TimeZoneInfo.TryFindSystemTimeZoneById(state.UserProfile.TimeZone, out var timeZone))
            state.SelectedTimeZone = null;
        else
            state.SelectedTimeZone = timeZone;
    }
}
