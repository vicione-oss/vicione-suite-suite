using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.Models;

public sealed class PermissionGridItem
{
    public required PermissionGridAccessLevel AccessLevel { get; set; }
    public required UserManagementClaim? Claim { get; set; }
    public required string Description { get; init; }
    public required string Feature { get; init; }
    public bool IsGroupingRow { get; set; }
    public required string ModuleId { get; init; }
}
