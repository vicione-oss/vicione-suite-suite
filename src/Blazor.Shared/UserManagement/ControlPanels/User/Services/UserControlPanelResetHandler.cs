using System.Globalization;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.UserManagement.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Services;

internal sealed class UserControlPanelResetHandler(IUserService userService) : IControlPanelResetHandler<UserControlPanelState>
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
        state.RoleToAdd = state.AvailableUserRoles?.FirstOrDefault();
    }

    private static async Task UpdatePermissionGridItems(UserControlPanelState state)
    {
        await state.UpdateAvailableClaims();
        state.UpdateGridItems();
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
    {
        if (state.UserProfile?.Language is null
            || Constants.SupportedCultures.All(c => c.Name != state.UserProfile.Language))
            state.SelectedCulture = null;
        else
            state.SelectedCulture = new CultureInfo(state.UserProfile.Language);
    }

    private static void ResetSelectedTimeZone(UserControlPanelState state)
    {
        if (state.UserProfile?.TimeZone is null
            || !TimeZoneInfo.TryFindSystemTimeZoneById(state.UserProfile.TimeZone, out var timeZone))
            state.SelectedTimeZone = null;
        else
            state.SelectedTimeZone = timeZone;
    }
}
