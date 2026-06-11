using Blazor.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Contracts;

namespace Blazor.Shared.UserManagement.Services;

public interface IExternalAccountService
{
    Task<ExternalUserAccount?> GetExternalUserAccount(SuiteUser user);

    Task<IUserManagementServiceResult> RemoveExternalAccount(SuiteUser user,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken = default);
}

public record ExternalUserAccount(string LoginProvider, string? DisplayName, string ProviderKey);
