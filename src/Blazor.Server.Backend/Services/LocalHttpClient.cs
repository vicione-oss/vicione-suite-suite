using Sdk.Client.Infrastructure;

namespace Blazor.Server.Backend.Services;

/// <summary>
/// Typed HttpClient for requests to the local server.
/// </summary>
internal class LocalHttpClient(HttpClient client) : ILocalHttpClient
{
    private readonly HttpClient _client = client;

    public HttpClient Client => _client;
}
