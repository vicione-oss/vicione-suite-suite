using System.IO.Abstractions;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Serialization;
using Core.Artifacts.Contracts;
using Core.Artifacts.Extensions;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Artifacts;

namespace Core.Artifacts.JFrog;

/// <inheritdoc />
public sealed partial class JFrogArtifactRepository(IFileSystem fileSystem,
    IHttpClientFactory httpClientFactory,
    IArtifactRepositoryOptionsProvider optionsProvider,
    ILogger<JFrogArtifactRepository> logger) : IArtifactRepository
{
    public const char UriSeparator = '/';
    private const string AqlApiPart = "api/search/aql";

    /// <inheritdoc />
    public IArtifactQueryBuilder CreateQueryBuilder()
        => new JFrogArtifactQueryBuilder();

    /// <inheritdoc />
    public Task<Stream> Download(IArtifact artifact, CancellationToken cancellationToken = default)
    {
        var sourceUri = GetDownloadUri(artifact);
        var client = CreateSourceClient(artifact.SourceKey);

        LogDownloadingArtifactToStream(logger, sourceUri);

        return client.GetStreamAsync(sourceUri, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DownloadToFile(IArtifact artifact, string targetFilePath, CancellationToken cancellationToken = default)
    {
        var sourceUri = GetDownloadUri(artifact);
        var client = CreateSourceClient(artifact.SourceKey);

        LogDownloadingArtifact(logger, sourceUri, targetFilePath);

        await using var httpStream = await client.GetStreamAsync(sourceUri, cancellationToken);
        await using var fs = fileSystem.FileStream.New(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);

        await httpStream.CopyToAsync(fs, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DownloadAndExtract(IArtifact artifact, string targetFolderPath, CancellationToken cancellationToken)
    {
        if (!fileSystem.Directory.Exists(targetFolderPath))
            fileSystem.Directory.CreateDirectory(targetFolderPath);

        var sourceUri = GetDownloadUri(artifact);
        var client = CreateSourceClient(artifact.SourceKey);

        LogDownloadingArtifactToPatch(logger, sourceUri, targetFolderPath);

        // Stream the archive to a temporary file on disk instead
        var tempFilePath = fileSystem.Path.Combine(targetFolderPath, $".{Guid.NewGuid():N}.download.tmp");

        try
        {
            await using (var httpStream = await client.GetStreamAsync(sourceUri, cancellationToken))
            await using (var tempFileStream = fileSystem.FileStream.New(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await httpStream.CopyToAsync(tempFileStream, cancellationToken);
            }

            await using var archiveStream = fileSystem.FileStream.New(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            foreach (var archiveEntry in archive.Entries)
            {
                if (archiveEntry.FullName.EndsWith('/'))
                {
                    var entryFolderPath = fileSystem.Path.Combine(targetFolderPath, archiveEntry.FullName);
                    fileSystem.Directory.CreateDirectory(entryFolderPath);
                    continue;
                }

                var entryFilePath = fileSystem.Path.Combine(targetFolderPath, archiveEntry.FullName);
                await archiveEntry.ExtractToFileAsync(entryFilePath, true, cancellationToken);
            }
        }
        finally
        {
            if (fileSystem.File.Exists(tempFilePath))
                fileSystem.File.Delete(tempFilePath);
        }
    }

    /// <inheritdoc />
    public async Task<IArtifactQueryResult> Query(string aqlQuery, CancellationToken cancellationToken = default)
    {
        var completeResult = new ArtifactQueryResult();
        var sources = CreateSourceConfigurations();
        var queryTasks = sources.Select(source => TryQuerySourceArtifactResult(source, aqlQuery, cancellationToken));
        var queryResults = await Task.WhenAll(queryTasks);

        foreach (var queryResult in queryResults)
        {
            AddSourceResult(completeResult, queryResult);
        }

        return completeResult;
    }

    private static void AddSourceResult(ArtifactQueryResult result, JFrogQueryResult sourceResult)
    {
        if (sourceResult.Error is not null)
        {
            result.WrapperErrors ??= [];
            result.WrapperErrors.Add(new ArtifactQueryError
            {
                Source = sourceResult.Source,
                Error = new Sdk.Messaging.ErrorInfo(1, sourceResult.Error.Message)
            });
            return;
        }

        result.Results.AddRange(sourceResult.Results);

        if (sourceResult.Range is not null)
        {
            result.WrapperRanges ??= [];
            result.WrapperRanges.Add(sourceResult.Range);
        }
    }

    private async Task<JFrogQueryResult> TryQuerySourceArtifactResult(ApiSourceConfig source, string aqlQuery, CancellationToken cancellationToken)
    {
        try
        {
            var repositoryQuery = JFrogArtifactQueryBuilder.InjectRepository(aqlQuery, source.RepositoryKey);
            var client = CreateSourceClient(source);

            LogPostArtifactQuery(logger, repositoryQuery);

            using var request = new HttpRequestMessage(HttpMethod.Post, AqlApiPart); // Relative path
            request.Content = new StringContent(repositoryQuery, Encoding.UTF8, MediaTypeNames.Text.Plain); // AQL is sent as plain text

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode(); // Throw exception if API call failed

            var aqlResult
                = await response.Content.ReadFromJsonAsync<JFrogQueryResult>(JFrogArtifactSerializerOptions.GetOptions(), cancellationToken: cancellationToken);

            if (aqlResult == null || aqlResult.Results.Count == 0)
                return new JFrogQueryResult { Source = source.SourceKey };

            EnrichArtifactWithSourceKey(aqlResult.Results, source);
            aqlResult.Source = source.SourceKey;

            return aqlResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailedToQueryArtifacts(logger, ex, source.RepositoryKey, source.SourceKey);
            return new JFrogQueryResult { Error = ex, Source = source.SourceKey };
        }
    }

    public async Task<List<string>> QueryRaw(string aqlQuery, CancellationToken cancellationToken = default)
    {
        var sources = CreateSourceConfigurations();
        var queryTasks = sources.Select(source => TryQuerySourceRaw(source, aqlQuery, cancellationToken));
        var queryResults = await Task.WhenAll(queryTasks);

        return [.. queryResults.Where(k => !string.IsNullOrEmpty(k)).Select(k => k!)];
    }

    private async Task<string?> TryQuerySourceRaw(ApiSourceConfig source, string aqlQuery, CancellationToken cancellationToken)
    {
        try
        {
            var repositoryQuery = JFrogArtifactQueryBuilder.InjectRepository(aqlQuery, source.RepositoryKey);
            var client = CreateSourceClient(source);

            LogPostRawArtifactQuery(logger, repositoryQuery);

            using var request = new HttpRequestMessage(HttpMethod.Post, AqlApiPart); // Relative path
            request.Content = new StringContent(repositoryQuery, Encoding.UTF8, MediaTypeNames.Text.Plain); // AQL is sent as plain text

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode(); // Throw exception if API call failed

            return await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailedToQueryRawData(logger, ex, source.RepositoryKey, source.SourceKey);
            return null;
        }
    }

    private static void EnrichArtifactWithSourceKey(IEnumerable<Artifact> artifacts, ApiSourceConfig sourceConfig)
    {
        foreach (var artifact in artifacts)
        {
            artifact.SourceKey = sourceConfig.SourceKey;
        }
    }

    /// <inheritdoc />
    public Uri GetDownloadUri(IArtifact artifact)
    {
        var sourceConfig = GetSourceConfig(artifact);

        // Ensure no double slashes and correct joining
        var repo = !string.IsNullOrWhiteSpace(artifact.Repository) ? artifact.Repository : sourceConfig.RepositoryKey;

        // Construct relative path carefully
        var fullPath = !string.IsNullOrWhiteSpace(artifact.Name) ? $"{artifact.Path}/{artifact.Name}" : artifact.Path;
        var relativePath = $"{repo}{UriSeparator}{fullPath}".TrimStart(UriSeparator);

        return new Uri(sourceConfig.BaseAddress, relativePath);
    }

    public IArtifact CreateArtifact(string path, string name, long? size = null, DateTimeOffset? modified = null, ArtifactKind artifactKind = ArtifactKind.File)
    {
        var sourceConfig = GetSourceConfig(string.Empty);

        return new Artifact
        {
            Path = path,
            Name = name,
            Size = size,
            Modified = modified,
            Kind = artifactKind,
            Repository = sourceConfig.RepositoryKey,
            SourceKey = sourceConfig.SourceKey
        };
    }

    private ApiSourceConfig GetSourceConfig(IArtifact artifact)
        => GetSourceConfig(artifact.SourceKey);

    private ApiSourceConfig GetSourceConfig(string? sourceKey)
    {
        // If we have only one source and source key is empty we take the available one
        var sources = CreateSourceConfigurations();
        var sourceConfig = string.IsNullOrEmpty(sourceKey) || sources.Count == 1
            ? sources.First()
            : sources.FirstOrDefault(k => k.SourceKey == sourceKey);

        return sourceConfig ?? throw new InvalidOperationException($"Artifact source '{sourceKey}' not available!");
    }

    private HttpClient CreateSourceClient(string source)
    {
        var sourceConfig = GetSourceConfig(source);

        return CreateSourceClient(sourceConfig);
    }

    /// <summary>
    /// Ensure using it without <langword>using</langword>
    /// </summary>
    /// <param name="sourceConfig"></param>    
    private HttpClient CreateSourceClient(ApiSourceConfig sourceConfig)
    {
        var client = httpClientFactory.CreateClient(sourceConfig.RepositoryKey);
        client.BaseAddress ??= sourceConfig.BaseAddress;

        if (client.DefaultRequestHeaders.Authorization is null)
            client.AddDefaultRequestHeaders(sourceConfig.ApiSource);

        return client;
    }

    private List<ApiSourceConfig> CreateSourceConfigurations()
    {
        var options = optionsProvider.GetOptions();
        var list = new List<ApiSourceConfig>();

        foreach (var source in options.Sources)
        {
            try
            {
                var config = new ApiSourceConfig(source);
                list.Add(config);
            }
            catch (Exception ex)
            {
                LogFailedToCreateApiConfig(logger, ex, source.Endpoint);
            }
        }

        return list;
    }

    public IEnumerable<string> GetSourceKeys() => CreateSourceConfigurations().Select(k => k.SourceKey);

    private class ApiSourceConfig
    {
        public ArtifactRepositorySourceOption ApiSource { get; }

        public string SourceKey { get; }

        public string RepositoryKey { get; }

        public Uri BaseAddress { get; }

        public ApiSourceConfig(ArtifactRepositorySourceOption apiSource)
        {
            ApiSource = apiSource;

            // Example Endpoint: https://your.artifactory.instance/artifactory/your-repo-key
            var endpointUri = new Uri(apiSource.Endpoint);
            RepositoryKey = endpointUri.Segments.LastOrDefault()?.Trim(UriSeparator)
                             ?? throw new InvalidOperationException(
                                 "Could not determine repository key from endpoint URL.");

            // Set BaseAddress for HttpClient to the Artifactory root
            // Ensures relative paths like /api/search/aql work correctly
            var absolutePath = endpointUri.AbsolutePath.Replace($"/{RepositoryKey}", "", StringComparison.Ordinal);
            var uriBuilder = new UriBuilder(endpointUri)
            {
                Path = absolutePath.TrimEnd(UriSeparator) + "/"
            };

            BaseAddress = uriBuilder.Uri;

            // Host:Port is not enough we need to take the whole uri + port to have a difference
            SourceKey = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{uriBuilder.Uri.AbsoluteUri}:{uriBuilder.Port}:{RepositoryKey}"));
        }
    }

    /// <summary>
    /// This maps the real results from JFrog source before we accumulate to ArtifactQueryResult
    /// </summary>
    internal class JFrogQueryResult
    {
        [JsonPropertyName("results")]
        public List<Artifact> Results { get; set; } = [];

        [JsonPropertyName("range")]
        public ArtifactQueryRange? Range { get; set; }

        public Exception? Error { get; set; }

        public string Source { get; set; } = string.Empty;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading artifact from '{SourceUri}' to '{TargetFilePath}'")]
    private static partial void LogDownloadingArtifact(ILogger logger, Uri sourceUri, string targetFilePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading artifact from '{SourceUri}' to patch '{TargetFolderPath}'")]
    private static partial void LogDownloadingArtifactToPatch(ILogger logger, Uri sourceUri, string targetFolderPath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading artifact from '{SourceUri}' to stream")]
    private static partial void LogDownloadingArtifactToStream(ILogger logger, Uri sourceUri);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Post artifact query: '{Query}'")]
    private static partial void LogPostArtifactQuery(ILogger logger, string query);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to query artifacts from repository='{Repository}' source='{Source}'")]
    private static partial void LogFailedToQueryArtifacts(ILogger logger, Exception exception, string repository, string source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Post raw artifact query: '{Query}'")]
    private static partial void LogPostRawArtifactQuery(ILogger logger, string query);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to query raw data from repository='{Repository}' source='{Source}'")]
    private static partial void LogFailedToQueryRawData(ILogger logger, Exception exception, string repository, string source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create source configuration for endpoint='{Endpoint}'")]
    private static partial void LogFailedToCreateApiConfig(ILogger logger, Exception exception, string endpoint);
}
