using Blazor.Shared.UserManagement.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Users.Services;

public sealed class UsersControlPanelResetHandler(IUserService userService) : IControlPanelResetHandler<UsersControlPanelState>
{
    public async Task Reset(UsersControlPanelState state, CancellationToken cancellationToken)
    {
        state.ResetSelectedUsers = true;
        state.Users = await userService.GetUsers(cancellationToken: cancellationToken);
        state.DeletingUsers.Clear();
    }
}
