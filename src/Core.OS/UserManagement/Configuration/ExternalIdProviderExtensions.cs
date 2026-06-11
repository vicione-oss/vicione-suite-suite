using Core.Shared.UserManagement.Configuration;

namespace Core.OS.UserManagement.Configuration;

public static class ExternalIdProviderExtensions
{
    extension(ExternalIdProvider provider)
    {
        public bool IsConfigured() => provider.ClientId != Constants.UnconfiguredClient;
    }
}
