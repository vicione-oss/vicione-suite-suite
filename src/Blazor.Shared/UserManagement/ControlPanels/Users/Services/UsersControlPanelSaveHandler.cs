using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Users.Services;

public sealed class UsersControlPanelSaveHandler(IUserService userService) : IControlPanelSaveHandler<UsersControlPanelState>
{
    public async Task<ISaveResult> Save(UsersControlPanelState state, CancellationToken cancellationToken)
    {
        foreach (var deletingUser in state.DeletingUsers.ToList())
        {
            var result = await userService.DeleteUser(deletingUser, cancellationToken);

            if (result is UserServiceErrorResult errorResult)
                return new SaveErrorResult(errorResult.ErrorMessage, errorResult.ErrorCode);

            state.DeletingUsers.Remove(deletingUser);
        }

        return new SaveSuccessResult();
    }
}
