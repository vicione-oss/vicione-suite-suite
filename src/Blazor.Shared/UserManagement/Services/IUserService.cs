using Blazor.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Contracts;
using Sdk.Messaging;
using Sdk.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.Services;

public interface IUserService
{
    event Func<UserProfile, CrudAction, Task>? UserChanged;

    Task<List<UserProfile>> GetUsers(UserName? userName = null, CancellationToken cancellationToken = default);
    Task<IUserManagementServiceResult> DeleteUser(UserProfile userProfile, CancellationToken cancellationToken = default);
    Task<IUserManagementServiceResult> CreateUser(UserProfile userProfile, CancellationToken cancellationToken = default);
    Task<IUserManagementServiceResult> UpdateUser(UserProfile userProfile, CancellationToken cancellationToken = default);
    Task<List<Role>> GetRoles(CancellationToken cancellationToken = default);
}
