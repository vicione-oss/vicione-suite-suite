using Core.OS.UserManagement.Configuration;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

/// <summary>
/// Drops this node's cached <see cref="OpenIdConnectOptions"/> so the next sign-in re-reads the
/// stored provider. <c>[ReadOnlyConsumer]</c> puts it on the per-instance queue, so every node clears its own.
/// </summary>
/// <remarks>
/// On a slave the event can arrive before the replicated row, so <see cref="ExternalIdProviderReplicationObserver"/>
/// clears the cache again once the row has been written.
/// </remarks>
[ReadOnlyConsumer]
public sealed partial class ExternalIdProviderChangedConsumer(
    IOptionsMonitorCache<OpenIdConnectOptions> optionsCache,
    ILogger<ExternalIdProviderChangedConsumer> logger)
    : IConsumer<ExternalIdProviderChanged>
{
    public Task Consume(ConsumeContext<ExternalIdProviderChanged> context)
    {
        optionsCache.TryRemove(DynamicExternalIdProviderOptions.OptionsName);

        LogOptionsCacheCleared(logger, context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Cleared the cached OpenID connect options after the provider changed, correlated by {CorrelationId}.")]
    private static partial void LogOptionsCacheCleared(ILogger<ExternalIdProviderChangedConsumer> logger, Guid correlationId);
}
