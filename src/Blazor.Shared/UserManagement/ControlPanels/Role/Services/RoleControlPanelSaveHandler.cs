using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

internal sealed class RoleControlPanelSaveHandler(IRoleService roleService)
        : IControlPanelSaveHandler<RoleControlPanelState>
{
    public async Task<ISaveResult> Save(RoleControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.Role is null)
            throw new InvalidOperationException("No role provided");

        IUserManagementServiceResult result;

        if (state.IsEditMode)
            result = await roleService.UpdateRole(state.Role, cancellationToken);
        else
            result = await roleService.CreateRole(state.Role, cancellationToken);

        if (result is UserManagementServiceSuccessResult)
        {
            state.PermissionEditContext = null;

            return new SaveSuccessResult();
        }

        if (result is UserManagementServiceErrorResult errorResult)
            return new SaveErrorResult(errorResult.ErrorMessage);

        throw new NotSupportedException("Result type unknown");
    }
}
