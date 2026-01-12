using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Shared.Instance.Contracts;
using Sdk.Client.Extensions;
using Sdk.Modules;

namespace Blazor.Wasm.Client.Infrastructure.Modules;

internal sealed class BackendModuleHttpClient(HttpClient httpClient) : IBackendModuleHttpClient
{
    private const string Api = "api/module";

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient = httpClient;

    public async Task<InstanceInformation?> GetLocalInstanceInfo(CancellationToken token = default)
    {
        var result = await _httpClient.GetFromJsonAsync<InstanceInformation>($"{Api}/localInstanceInfo/", token);
        return result;
    }

    public async Task<List<ModuleMetadata>> GetModuleMetadata(CancellationToken token = default)
    {
        var result = await _httpClient.GetFromJsonAsync<List<ModuleMetadata>>($"{Api}/metadata/", token);
        if (result is null)
            return [];

        return result;
    }

    public async Task<ModuleMetadata?> GetModuleMetadata(string moduleId, CancellationToken token = default)
    {
        var builder = _httpClient.CreateUriBuilder().WithPath($"{Api}/metadata/{moduleId}");
        var response = await _httpClient.GetAsync(builder.Uri, token);
        if (!response.IsSuccessStatusCode)
            return null;

        var responseBody = await response.Content.ReadAsStringAsync(token);
        if (string.IsNullOrEmpty(responseBody))
            return null;

        return JsonSerializer.Deserialize<ModuleMetadata>(responseBody, _jsonSerializerOptions);
    }

    public async Task<byte[]> LoadClientModulesArchive(IEnumerable<string?> assemblyNames,
        CancellationToken token = default)
    {
        var builder = _httpClient.CreateUriBuilder().WithPath($"{Api}/load");
        var response = await _httpClient.PostAsJsonAsync(builder.Uri, assemblyNames, token);

        if (!response.IsSuccessStatusCode)
            return [];

        return await response.Content.ReadAsByteArrayAsync(token);
    }

    public Task<byte[]> LoadClientModuleResourcesArchive(CultureInfo cultureInfo, CancellationToken token = default)
    {
        var builder = _httpClient.CreateUriBuilder().WithPath($"{Api}/resource/{cultureInfo.Name}");
        return _httpClient.GetByteArrayAsync(builder.Uri, token);
    }
}
