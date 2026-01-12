using System.Globalization;
using System.Security.Principal;
using Blazor.Shared;
using Blazor.Shared.Services;
using Core.Shared.Messaging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Services;

public sealed partial class UiEventPublisher<T>(IMemoryCache cache, IUiEventSubscriptionRegistry<T> registry, ILogger<UiEventPublisher<T>> logger) : IUiEventPublisher<T>, IUiEventSubscriptionHolder<T>
    where T : class, IEvent
{
    public IDisposable Connect(IEventConsumer<T> handler, IIdentity? identity = null)
        => registry.Connect(handler, identity);

    public async Task PublishUiEvent(T eventToPublish,
        Guid? correlationId,
        CancellationToken cancellationToken = default)
    {
        var defaultUiCulture = CultureInfo.CurrentUICulture;
        var defaultCulture = CultureInfo.CurrentCulture;
        var eventFullName = eventToPublish.GetType().FullName;

        // Because we trigger the event subscription call in backend we are not linked to any circuit
        // and therefore the culture is not set for user. We have to do that manually per subscription
        await registry.ForEachAsync(async (consumer, identity) =>
        {
            try
            {
                if (identity is not null)
                {
                    var cacheKey = Constants.GetUserCultureCacheKey(identity.Name!);

                    if (cache.TryGetValue<string>(cacheKey, out var cultureString) && !string.IsNullOrEmpty(cultureString))
                    {
                        // Set specific user culture to render correct language
                        var culture = new CultureInfo(cultureString);
                        if (culture != CultureInfo.CurrentCulture)
                        {
                            CultureInfo.CurrentUICulture = culture;
                            CultureInfo.CurrentCulture = culture;
                        }
                    }
                    else
                    {
                        // Users without a specific language settings will be rendered with default language
                        CultureInfo.CurrentUICulture = defaultUiCulture;
                        CultureInfo.CurrentCulture = defaultCulture;
                    }
                }

                LogConsumeEvent(logger,
                    consumer.GetType().FullName,
                    eventFullName,
                    identity?.Name ?? "default",
                    CultureInfo.CurrentUICulture.Name);

                await consumer.Consume(new ClientContext<T>(eventToPublish, correlationId), cancellationToken);
            }
            catch (Exception e)
            {
                LogConsumeEventError(logger, e, consumer.GetType().FullName, eventFullName);
            }
        }).ConfigureAwait(false);

        // ensure we don't change the culture after handling the last event
        CultureInfo.CurrentUICulture = defaultUiCulture;
        CultureInfo.CurrentCulture = defaultCulture;
    }

    [LoggerMessage(1, LogLevel.Debug, "UI consumer={Consumer} event={Event} user={User} with uiCulture={UiCulture}")]
    private static partial void LogConsumeEvent(ILogger<UiEventPublisher<T>> logger, string? Consumer, string? Event, string User, string UiCulture);

    [LoggerMessage(2, LogLevel.Error, "UI consumer={Consumer} failed on event={Event}")]
    private static partial void LogConsumeEventError(ILogger<UiEventPublisher<T>> logger, Exception ex, string? Consumer, string? Event);
}

