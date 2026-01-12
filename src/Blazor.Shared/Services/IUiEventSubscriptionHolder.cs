using System.Security.Principal;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Shared.Services;

public interface IUiEventSubscriptionHolder<T> where T : class, IEvent
{
    IDisposable Connect(IEventConsumer<T> handler, IIdentity? identity = null);
}
