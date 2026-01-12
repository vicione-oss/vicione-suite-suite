using Blazor.Wasm.Client.Infrastructure.SignalR;
using Core.Shared.Messaging;
using Sdk.Messaging;

namespace Blazor.Wasm.Backend.SignalR;

public sealed class UiEventToSignalRPublisher<T>(IMessageHub messageHub) : IUiEventPublisher<T> where T : IEvent
{
    public Task PublishUiEvent(T? eventToPublish,
        Guid? correlationId,
        CancellationToken cancellationToken = default)
    {
        if (eventToPublish is null)
            return Task.CompletedTask;

        var envelope = SignalRMessageFactory.Envelop(eventToPublish, correlationId);
        return messageHub.SendEvent(envelope);
    }
}
