namespace Blazor.Wasm.Client.Infrastructure.SignalR;

public interface IMessageHubClient
{
    Task ReceiveEvent(SignalRMessageEnvelope domainEvent);
}
