using Blazor.Shared.Services;
using Core.Shared.Messaging;
using MassTransit.Util;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Services;

public sealed class UiEventPublisher<T>(ILogger<UiEventPublisher<T>> logger) : IUiEventPublisher<T>, IUiEventSubscriptionHolder<T>
    where T : class, IEvent
{
    private static readonly Connectable<IEventConsumer<T>> _subscriptions = new();

    public async Task PublishUiEvent(T eventToPublish,
        Guid? correlationId,
        CancellationToken cancellationToken = default)
    {
        await _subscriptions.ForEachAsync(s =>
        {
            try
            {
                return s.Consume(new ClientContext<T>(eventToPublish, correlationId), cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error when executing consume of {EventName} in {Handler} - skipping", typeof(T).Name, s.GetType().Name);
                return Task.CompletedTask;
            }
        }).ConfigureAwait(false);
    }

    public IDisposable Connect(IEventConsumer<T> handler)
    {
        return _subscriptions.Connect(handler);
    }
}

