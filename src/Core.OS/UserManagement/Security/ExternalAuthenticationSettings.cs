using Core.OS.DbContext;
using Core.OS.UserManagement.Configuration;
using Core.Shared.Security;
using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Security;

public class ExternalAuthenticationSettings(
    IOptions<ExternalIdProviderOptions> options,
    ApplicationDbContext applicationDbContext,
    ILogger<ExternalAuthenticationSettings> logger)
    : IExternalAuthenticationSettings
{
    public Task<bool> IsExternalAuthenticationProviderConfigured()
    {
        var externalIdProvider = GetExternalIdProvider();

        return Task.FromResult(
            externalIdProvider is not null &&
            externalIdProvider.IsConfigured());
    }

    private ExternalIdProvider? GetExternalIdProvider()
        => GetProvidersInDatabase().FirstOrDefault() ?? GetProviderInConfiguration();

    private ExternalIdProvider? GetProviderInConfiguration()
    {
        var providers = options.Value.Providers;

        if (providers.Count > 1)
            logger.LogWarning(
                "Multiple external ids are configured {@Providers}. Only one is currently supported. Picking the first one.",
                providers.Select(x => x.Name));

        return providers.FirstOrDefault();
    }

    private IQueryable<ExternalIdProvider> GetProvidersInDatabase()
        => from externalIdProvider in applicationDbContext.ExternalIdProviders
            select new ExternalIdProvider
            {
                Authority = externalIdProvider.Authority,
                ClientId = externalIdProvider.ClientId,
                ClientSecret = externalIdProvider.ClientSecret,
                Name = externalIdProvider.Name
            };
}
