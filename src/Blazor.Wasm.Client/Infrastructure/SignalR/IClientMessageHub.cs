namespace Blazor.Wasm.Client.Infrastructure.SignalR;

public interface IClientMessageHub : IMessageHub
{
    event EventHandler<SignalRMessageEnvelope>? EventReceived;

    Task Start();

    Task SendCommand(SignalRMessageEnvelope envelope, CancellationToken cancellationToken);

    Task<SignalRMessageEnvelope> SendRequest(SignalRMessageEnvelope envelope, CancellationToken cancellationToken);
}
