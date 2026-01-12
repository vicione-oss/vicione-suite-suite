using System.Security.Principal;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Services;

public interface IUiEventSubscriptionRegistry<T> where T : class, IEvent
{
    IDisposable Connect(IEventConsumer<T> consumer, IIdentity? identity = null);
    Task ForEachAsync(Func<IEventConsumer<T>, IIdentity?, Task> handler);
}

