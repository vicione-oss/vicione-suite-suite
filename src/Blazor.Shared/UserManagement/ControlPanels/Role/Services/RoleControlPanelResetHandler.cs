using Blazor.Shared.UserManagement.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

internal sealed class RoleControlPanelResetHandler(IRoleService roleService,
    IGridItemService gridItemService,
    IAvailableClaimsService availableClaimsService) : IControlPanelResetHandler<RoleControlPanelState>
{
    public async Task Reset(RoleControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginUpdate();

        try
        {
            await UpdateRole(state);

            await UpdatePermissionGridItems(state);

            state.PermissionEditContext = null;
        }
        finally
        {
            state.EndUpdate();
        }
    }

    private async Task UpdatePermissionGridItems(RoleControlPanelState state)
    {
        await availableClaimsService.UpdateAvailableClaims(state);
        gridItemService.UpdateGridItems(state);
    }

    private async Task UpdateRole(RoleControlPanelState state)
    {
        if (state.RoleName is not null)
        {
            var role = await GetRole(state);

            if (role != null)
                state.Role = role;
            else
                throw new InvalidOperationException(Localization.RoleControlPanelResetHandler.FailedToFetchRole);
        }
        else
        {
            state.Role = new Sdk.UserManagement.Contracts.Role();
        }
    }

    private async Task<Sdk.UserManagement.Contracts.Role?> GetRole(RoleControlPanelState state)
    {
        var roles = await roleService.GetAvailableRoles();

        return roles.FirstOrDefault(r => r.Name == state.RoleName);
    }
}
