namespace Blazor.Wasm.Client.Infrastructure.SignalR;

public interface IMessageHub
{
    Task SendEvent(SignalRMessageEnvelope envelope);

    Task SendCommand(SignalRMessageEnvelope envelope);

    Task<SignalRMessageEnvelope> SendRequest(SignalRMessageEnvelope envelope);
}
