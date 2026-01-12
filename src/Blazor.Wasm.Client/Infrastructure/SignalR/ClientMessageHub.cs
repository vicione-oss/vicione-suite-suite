using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Blazor.Wasm.Client.Infrastructure.SignalR;

internal sealed class ClientMessageHub(NavigationManager navigationManager, ILogger<ClientMessageHub> logger) :
    IClientMessageHub, IMessageHubClient, IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly HubConnection _connection = ConnectionFactoryHelper.CreateConnection(navigationManager.ToAbsoluteUri(Shared.Constants.SignalRHubEndpoint));
    private readonly ILogger<ClientMessageHub> _logger = logger;
    private IDisposable? _channelSubscription;

    public event EventHandler<SignalRMessageEnvelope>? EventReceived;

    public async ValueTask DisposeAsync()
    {
        await _connection.StopAsync(_cancellationTokenSource.Token);
        await _connection.DisposeAsync();
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
        _channelSubscription?.Dispose();
    }

    public Task Start()
    {
        if (_connection.State == HubConnectionState.Connected)
            return Task.CompletedTask;

        _logger.LogInformation("Connecting to backend SignalR hub");

        _channelSubscription ??=
            _connection.BindOnInterface<SignalRMessageEnvelope>(x => x.ReceiveEvent, ReceiveEvent);
        return _connection.StartAsync();
    }

    /// <summary>
    /// Sends an Event as if it came from the Backend. Use at your own peril
    /// </summary>
    /// <param name="envelope"></param>
    /// <returns></returns>
    public Task SendEvent(SignalRMessageEnvelope envelope) => ReceiveEvent(envelope);

    public Task SendCommand(SignalRMessageEnvelope envelope)
    {
        return SendCommand(envelope, CancellationToken.None);
    }

    public Task SendCommand(SignalRMessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ThrowOnClosedConnection();

        return _connection.InvokeAsync(nameof(IMessageHub.SendCommand), envelope, cancellationToken);
    }

    public Task<SignalRMessageEnvelope> SendRequest(SignalRMessageEnvelope envelope)
    {
        return SendRequest(envelope, CancellationToken.None);
    }

    public Task<SignalRMessageEnvelope> SendRequest(SignalRMessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ThrowOnClosedConnection();

        return _connection.InvokeAsync<SignalRMessageEnvelope>(
            nameof(IMessageHub.SendRequest), envelope, cancellationToken
        );
    }

    public Task ReceiveEvent(SignalRMessageEnvelope envelope)
    {
        _logger.LogDebug("Received {MessageName} correlationId:{CorrelationId}",
            envelope.PayloadTypeFullName,
            envelope.CorrelationId);

        // pass to client mediator
        EventReceived?.Invoke(this, envelope);
        return Task.CompletedTask;
    }

    private void ThrowOnClosedConnection()
    {
        if (_connection.State != HubConnectionState.Connected)
            throw new InvalidOperationException($"SignalR hub is not connected (current state: {_connection.State})- please refresh the page");
    }
}
