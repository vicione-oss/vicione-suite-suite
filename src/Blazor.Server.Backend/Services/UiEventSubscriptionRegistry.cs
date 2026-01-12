using System.Security.Principal;
using MassTransit.Util;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Services;

public class UiEventSubscriptionRegistry<T> : IUiEventSubscriptionRegistry<T> where T : class, IEvent
{
    private record EventConsumerHandler(IEventConsumer<T> Consumer, IIdentity? Identity);

    private readonly Connectable<EventConsumerHandler> _subscriptions = new();

    public IDisposable Connect(IEventConsumer<T> consumer, IIdentity? identity = null)
        => _subscriptions.Connect(new EventConsumerHandler(consumer, identity));

    public Task ForEachAsync(Func<IEventConsumer<T>, IIdentity?, Task> handler)
        => _subscriptions.ForEachAsync(h => handler(h.Consumer, h.Identity));
}

