using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Roles.Services;

public sealed class RolesControlPanelSaveHandler(IRoleService roleService) : IControlPanelSaveHandler<RolesControlPanelState>
{
    public async Task<ISaveResult> Save(RolesControlPanelState state, CancellationToken cancellationToken)
    {
        foreach (var deletingRole in state.DeletingRoles.ToList())
        {
            var result = await roleService.DeleteRole(deletingRole, cancellationToken);

            if (result is UserManagementServiceErrorResult errorResult)
                return new SaveErrorResult(errorResult.ErrorMessage, errorResult.ErrorCode);

            state.DeletingRoles.Remove(deletingRole);
        }

        return new SaveSuccessResult();
    }
}
