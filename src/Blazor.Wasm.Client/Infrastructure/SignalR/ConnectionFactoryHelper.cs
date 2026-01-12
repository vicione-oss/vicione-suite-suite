using Microsoft.AspNetCore.SignalR.Client;

namespace Blazor.Wasm.Client.Infrastructure.SignalR;

internal static class ConnectionFactoryHelper
{
    public static HubConnection CreateConnection(Uri url)
        => new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();
}
