using System.Collections.Concurrent;
using Blazor.Wasm.Client.Infrastructure.SignalR;
using MassTransit;
using MassTransit.Internals;
using MassTransit.Util;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Wasm.Client.Infrastructure.Mediator;

internal sealed class UiMediator : IUiMediator, IDisposable
{
    private readonly ConcurrentDictionary<Type, Connectable<object>> _subscriptions = new();

    private readonly IClientMessageHub _hub;
    private readonly ILogger<UiMediator> _logger;

    public UiMediator(IClientMessageHub hub, ILogger<UiMediator> logger)
    {
        _hub = hub;
        _logger = logger;

        // we need to get the received events to publish them in client
        _hub.EventReceived += OnHubEventReceived;
    }

    public void Dispose()
    {
        _hub.EventReceived -= OnHubEventReceived;
    }

    public IDisposable Register<TEvent>(IEventConsumer<TEvent> handler) where TEvent : class, IEvent
    {
        var subscriptions = _subscriptions.GetOrAdd(typeof(TEvent), new Connectable<object>());
        return subscriptions.Connect(handler);
    }


    public Task Send<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : class, ICommand
        => SendInternal(command, null, cancellationToken);

    public Task Send<TCommand>(TCommand command, Guid instanceId, CancellationToken cancellationToken = default)
        where TCommand : class, IInstanceDependentCommand
        => SendInternal(command, instanceId, cancellationToken);

    private async Task SendInternal<TCommand>(TCommand command, Guid? instanceId, CancellationToken cancellationToken)
        where TCommand : class, CorrelatedBy<Guid>, IRoutableMessage
    {
        var envelope = SignalRMessageFactory.Envelop(command, instanceId, command.CorrelationId);

        _logger.LogInformation("Sending '{Command}' correlationId {CorrelationId}",
            envelope.PayloadTypeFullName,
            envelope.CorrelationId);

        await _hub.SendCommand(envelope, cancellationToken);
    }

    /// <summary>
    /// send the request to the hub and return the response if the request was successful
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    /// <exception cref="InvalidOperationException">if request failed</exception>
    /// <returns></returns>
    public async Task<TResponse> Request<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TResponse>
        where TResponse : class, IResponse
        => (TResponse)await RequestInternal(request, cancellationToken).ConfigureAwait(false);

    private async Task<object> RequestInternal(IRoutableMessage query, CancellationToken cancellationToken = default)
    {
        var envelope = SignalRMessageFactory.Envelop(query);

        _logger.LogDebug("Sending request '{Request}' correlationId {CorrelationId}",
            envelope.PayloadTypeFullName,
            envelope.CorrelationId);

        var resultMessage = await _hub.SendRequest(envelope, cancellationToken);
        if (resultMessage.Failed)
        {
            _logger.LogError("Request failed on requestId {CorrelationId}",
                resultMessage.CorrelationId);

            throw new InvalidOperationException($"Request for {envelope.PayloadTypeFullName} failed on backend");
        }

        _logger.LogDebug("Received response '{Response}' correlationId {CorrelationId}",
            resultMessage.PayloadTypeFullName,
            resultMessage.CorrelationId);

        return resultMessage.DeserializePayload(out _);
    }


    private async void OnHubEventReceived(object? sender, SignalRMessageEnvelope envelope)
    {
        // deserialize the received event from envelope  
        var ev = envelope.DeserializePayload(out var evType);

        // we have to dynamically create the context for the event to pass correlation id to consumer
        var contextType = typeof(ClientContext<>).MakeGenericType(evType);
        var context = Activator.CreateInstance(contextType, ev, envelope.CorrelationId) ??
            throw new InvalidOperationException(ContextIsNull(evType));

        _logger.LogDebug("Received event '{Event}' correlationId '{Source}' over signalR",
            evType.Name,
            envelope.CorrelationId);

        if (!_subscriptions.TryGetValue(evType, out var subscriptions))
            return;

        var handlerType = typeof(IEventConsumer<>).MakeGenericType(evType);

        await subscriptions.ForEachAsync(s =>
        {
            try
            {
                return (Task)handlerType
                    .GetMethod("Consume")!
                    .Invoke(s, [context, CancellationToken.None])!;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error when executing consume of {EventName} in {Handler} - skipping", evType.Name, handlerType.Name);
                return Task.CompletedTask;
            }
        }).ConfigureAwait(false);
    }

    private static string ContextIsNull(Type requestType)
        => $"Failed to create event context on type {requestType.GetTypeName()}";
    Task<TResponse> IUiMediator.Request<TRequest, TResponse>(TRequest request, Guid instanceId, CancellationToken cancellationToken) => throw new NotImplementedException();
}
