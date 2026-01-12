using System.Net.Http.Json;
using System.Text.Json;
using Blazor.Shared.Services;
using Sdk.Client.Extensions;

namespace Blazor.Wasm.Client.Infrastructure.Logging;

internal sealed class BackendLogHttpService(HttpClient httpClient, ILogger<BackendLogHttpService> logger) : IBackendLogService
{
    private const string Api = "api/log";

    public async Task<LogLevel> GetLogLevel()
    {
        try
        {
            var level = await httpClient.GetFromJsonAsync<string>(UriWithPath($"{Api}/level"));
            return !string.IsNullOrEmpty(level) ? Enum.Parse<LogLevel>(level) : LogLevel.Information;
        }
        catch (JsonException e)
        {
            logger.LogError(e, nameof(GetLogLevel));
        }

        return LogLevel.Information;
    }

    public async Task SetLogLevel(LogLevel level)
    {
        var builder = httpClient.CreateUriBuilder().WithPath($"{Api}/level/{level}");
        await httpClient.GetAsync(builder.Uri);
    }

    public Task<IEnumerable<string>> GetLogPaths()
        => GetLogsPaths(UriWithPath($"{Api}/paths"));

    private async Task<IEnumerable<string>> GetLogsPaths(Uri uri)
    {
        IEnumerable<string>? logNames;
        try
        {
            logNames = await httpClient.GetFromJsonAsync<IEnumerable<string>>(uri);
        }
        catch (JsonException e)
        {
            logger.LogError(e, nameof(GetLogsPaths));
            logNames = null;
        }

        return logNames ?? [];
    }

    public Task<Stream> GetLog(string logPath, CancellationToken token = default)
    {
        var uri = UriWithPath($"{Api}/paths/{Uri.EscapeDataString(logPath)}");

        return httpClient.GetStreamAsync(uri, token);
    }

    private Uri UriWithPath(string path) => httpClient.CreateUriBuilder().WithPath(path).Uri;
}
