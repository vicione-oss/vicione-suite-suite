namespace Blazor.Shared.UserManagement.Services;

public interface IExternalAccountService
{
    Task<ExternalUserAccount?> GetExternalUserAccount(string userName);
}

public record ExternalUserAccount(string LoginProvider, string? DisplayName, string ProviderKey);
