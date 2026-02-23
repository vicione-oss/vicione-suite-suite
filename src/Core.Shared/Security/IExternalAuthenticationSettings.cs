namespace Core.Shared.Security;

public interface IExternalAuthenticationSettings
{
    Task<bool> IsExternalAuthenticationProviderConfigured();
}
