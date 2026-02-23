using Core.Shared.Security;
using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Security;

public class ExternalAuthenticationSettings(
    IOptions<ExternalIdProviderOptions> options,
    ILogger<ExternalAuthenticationSettings> logger)
    : IExternalAuthenticationSettings
{
    public Task<bool> IsExternalAuthenticationProviderConfigured()
    {
        var externalIdProvider = GetExternalIdProvider();

        return Task.FromResult(externalIdProvider is not null);
    }

    private ExternalIdProvider? GetExternalIdProvider()
    {
        var externalIdProviders = options.Value.Providers;
        if (!externalIdProviders.Any())
            return null;

        if (externalIdProviders.Count > 1)
            logger.LogWarning(
                "Multiple external ids are configured {@Providers}. Only one is currently supported. Picking the first one.",
                externalIdProviders.Select(x => x.Name));

        return externalIdProviders.FirstOrDefault();
    }
}
