using System.Net.Http.Json;
using Blazor.Wasm.Client.Infrastructure.Security.Contracts;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Sdk.Client.Extensions;

namespace Blazor.Wasm.Client.Infrastructure.Security;

public interface IAuthApi
{
    Task<CurrentUser?> CurrentUserInfo();

    Task Login(LoginRequest login);

    Task Logout();
}

public sealed class AuthApi(IHttpClientFactory httpClientFactory) : IAuthApi
{
    private const string Api = "/api/auth";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task Login(LoginRequest login)
    {
        using var client = _httpClientFactory.CreateBackendClient();
        var builder = client.CreateUriBuilder().WithPath($"{Api}/login");

        var result = await client.PostAsJsonAsync(builder.Uri, login);
        if (result.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var response = await result.Content.ReadAsStringAsync();
            throw new InvalidOperationException(response);
        }
        result.EnsureSuccessStatusCode();
    }

    public async Task Logout()
    {
        using var client = _httpClientFactory.CreateBackendClient();
        var builder = client.CreateUriBuilder().WithPath($"{Api}/logout");

        var result = await client.PostAsync(builder.Uri, null);
        result.EnsureSuccessStatusCode();
    }

    public async Task<CurrentUser?> CurrentUserInfo()
    {
        using var client = _httpClientFactory.CreateBackendClient();
        var builder = client.CreateUriBuilder().WithPath($"{Api}/currentuserinfo");

        var result = await client.GetFromJsonAsync<CurrentUser>(builder.Uri);
        return result;
    }
}

public class CookieHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        return await base.SendAsync(request, cancellationToken);
    }
}
