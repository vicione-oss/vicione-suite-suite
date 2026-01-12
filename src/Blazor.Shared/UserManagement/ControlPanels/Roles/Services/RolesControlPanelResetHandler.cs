using Blazor.Shared.UserManagement.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Roles.Services;

public sealed class RolesControlPanelResetHandler(IRoleService roleService) : IControlPanelResetHandler<RolesControlPanelState>
{
    public async Task Reset(RolesControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginUpdate();

        try
        {
            state.ResetSelectedRoles = true;
            state.Roles = (List<Sdk.UserManagement.Contracts.Role>)await roleService.GetAvailableRoles(cancellationToken);
            state.DeletingRoles.Clear();
        }
        finally
        {
            state.EndUpdate();
        }
    }
}
