using Core.Shared.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.Services;

public interface IRolesProvider
{
    Task<IEnumerable<Role>> GetAvailableRoles(CancellationToken cancellationToken = default);
}
