using Core.Shared.UserManagement.Configuration;
using Microsoft.Extensions.Configuration;

namespace Core.Shared.Extensions;

public static class IConfigurationExtensions
{
    extension(IConfiguration config)
    {
        public ExternalIdProviderOptions? GetExternalIdProviderOptions()
            => config.GetSection(ExternalIdProviderOptions.ConfigSection).Get<ExternalIdProviderOptions>();
    }
}
