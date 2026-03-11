using Sdk.Client.Infrastructure;

namespace Blazor.Server.Backend.Services;

/// <summary>
/// A implementation of typed HttpClient that is used to make requests to the local server.
/// </summary>
/// <param name="client"></param>
internal class LocalHttpClient(HttpClient client) : ILocalHttpClient
{
    private readonly HttpClient _client = client;

    public HttpClient Client => _client;
}
