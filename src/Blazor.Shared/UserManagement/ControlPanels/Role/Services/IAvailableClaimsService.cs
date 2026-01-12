namespace Blazor.Shared.UserManagement.ControlPanels.Role.Services;

internal interface IAvailableClaimsService
{
    Task UpdateAvailableClaims(RoleControlPanelState state);
}

