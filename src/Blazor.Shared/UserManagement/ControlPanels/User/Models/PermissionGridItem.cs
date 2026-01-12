using Core.Shared.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.ControlPanels.User.Models;

public sealed class PermissionGridItem
{
    public required PermissionGridAccessLevel AccessLevel { get; set; }
    public required UserProfileClaim? Claim { get; set; }
    public required string Description { get; init; }
    public required string Feature { get; init; }
    public bool IsGroupingRow { get; set; }
    public required string ModuleId { get; init; }
}
