using Blazor.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Contracts;
using Sdk.Messaging;

namespace Blazor.Shared.UserManagement.Services;

public interface IUserService
{
    event Func<UserProfile, CrudAction, Task>? UserChanged;

    Task<List<UserProfile>> GetUsers(UserName? userName = null, CancellationToken cancellationToken = default);
    Task<IUserServiceResult> DeleteUser(UserProfile userProfile, CancellationToken cancellationToken = default);
    Task<IUserServiceResult> CreateUser(UserProfile userProfile, CancellationToken cancellationToken = default);
    Task<IUserServiceResult> UpdateUser(UserProfile userProfile, CancellationToken cancellationToken = default);
    Task<List<string>> GetRoles(CancellationToken cancellationToken = default);
}
