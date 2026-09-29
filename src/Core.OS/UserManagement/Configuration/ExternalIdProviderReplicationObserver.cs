using Core.OS.Persistence;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Configuration;

/// <summary>
/// Drops a slave's cached <see cref="OpenIdConnectOptions"/> once replication has written the stored provider, which
/// <see cref="Consumers.ExternalIdProviderChangedConsumer"/> may clear before the row arrives.
/// </summary>
internal sealed class ExternalIdProviderReplicationObserver(IOptionsMonitorCache<OpenIdConnectOptions> optionsCache)
    : IReplicationObserver
{
    private static readonly string ProviderEntityType = typeof(ExternalIdProvider).FullName!;

    public void ChangeSetApplied(string contextType, IReadOnlySet<string> entityTypeNames)
    {
        if (entityTypeNames.Contains(ProviderEntityType))
            ClearCachedOptions();
    }

    public void Resynchronized() => ClearCachedOptions();

    private void ClearCachedOptions() => optionsCache.TryRemove(DynamicExternalIdProviderOptions.OptionsName);
}
