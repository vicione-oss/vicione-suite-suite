using Blazor.Shared.UserManagement.Contracts;
using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.Services;

public interface IRoleService
{
    Task<IUserManagementServiceResult> CreateRole(Role role, CancellationToken cancellationToken = default);
    Task<IUserManagementServiceResult> DeleteRole(Role role, CancellationToken cancellationToken = default);
    Task<IUserManagementServiceResult> UpdateRole(Role role, CancellationToken cancellationToken = default);
    Task<IEnumerable<Role>> GetAvailableRoles(CancellationToken cancellationToken = default);
}
