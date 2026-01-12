using System.IO.Abstractions;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Core.Module.Options;
using Microsoft.Extensions.Options;
using Sdk.Backend.ArtifactApi;

namespace Core.Module.JFrog;

/// <inheritdoc />
public class JFrogArtifactQueryApi : IArtifactQueryApi
{
    public const char UriSeparator = '/';
    private const string AqlApiPart = "api/search/aql";
    private const string Repository = "vicione-suite";
    private readonly string _repositoryKey;

    private readonly HttpClient _client;
    private readonly IFileSystem _fileSystem;

    public JFrogArtifactQueryApi(IFileSystem fileSystem, HttpClient httpClient, IOptions<ModuleApiOptions> apiOptions)
    {
        _fileSystem = fileSystem;
        _client = httpClient;

        // Example Endpoint: https://your.artifactory.instance/artifactory/your-repo-key
        var endpointUri = new Uri(apiOptions.Value.Endpoint);
        _repositoryKey = endpointUri.Segments.LastOrDefault()?.Trim(UriSeparator)
                         ?? throw new InvalidOperationException(
                             "Could not determine repository key from endpoint URL.");

        _client.AddDefaultRequestHeaders(apiOptions.Value);

        // Set BaseAddress for HttpClient to the Artifactory root
        // Ensures relative paths like /api/search/aql work correctly
        var absolutePath = endpointUri.AbsolutePath.Replace($"/{_repositoryKey}", "", StringComparison.Ordinal);
        var uriBuilder = new UriBuilder(endpointUri)
        {
            Path = absolutePath.TrimEnd(UriSeparator) + "/"
        };

        _client.BaseAddress = uriBuilder.Uri;
    }

    /// <inheritdoc />
    public IArtifactQueryBuilder CreateQueryBuilder()
        => new JFrogArtifactQueryBuilder(Repository);

    /// <inheritdoc />
    public Task<Stream> DownloadStream(IArtifactItem artifact, CancellationToken cancellationToken = default)
    {
        var sourceUri = CreateDownloadUri(artifact);

        return _client.GetStreamAsync(sourceUri, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DownloadToFile(IArtifactItem artifact, string targetFilePath, CancellationToken cancellationToken = default)
    {
        await using var httpStream = await DownloadStream(artifact, cancellationToken);
        await using var fs = _fileSystem.FileStream.New(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);

        await httpStream.CopyToAsync(fs, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IArtifactQueryResult> ExecuteQuery(string aqlQuery, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AqlApiPart); // Relative path
        request.Content = new StringContent(aqlQuery, Encoding.UTF8, MediaTypeNames.Text.Plain); // AQL is sent as plain text

        var response = await _client.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode(); // Throw exception if API call failed

        var aqlResult
            = await response.Content.ReadFromJsonAsync<ArtifactQueryResult>(cancellationToken: cancellationToken);

        return aqlResult as IArtifactQueryResult ?? throw new InvalidOperationException("Query returned no result");
    }

    /// <inheritdoc />
    public async Task DownloadAndExtract(IArtifactItem artifact, string targetFolderPath, CancellationToken cancellationToken)
    {
        var sourceUri = CreateDownloadUri(artifact);

        if (!_fileSystem.Directory.Exists(targetFolderPath))
            _fileSystem.Directory.CreateDirectory(targetFolderPath);

        await using var httpStream = await _client.GetStreamAsync(sourceUri, cancellationToken);
        await using var ms = new MemoryStream();

        await httpStream.CopyToAsync(ms, cancellationToken);
        await httpStream.DisposeAsync();

        using var archive = new ZipArchive(ms);
        foreach (var archiveEntry in archive.Entries)
        {
            if (archiveEntry.FullName.EndsWith('/'))
            {
                var entryFolderPath = _fileSystem.Path.Combine(targetFolderPath, archiveEntry.FullName);
                _fileSystem.Directory.CreateDirectory(entryFolderPath);
                continue;
            }

            var entryFilePath = _fileSystem.Path.Combine(targetFolderPath, archiveEntry.FullName);
            archiveEntry.ExtractToFile(entryFilePath, true);
        }
    }

    public async Task<string> ExecuteQueryRaw(string aqlQuery, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AqlApiPart); // Relative path
        request.Content = new StringContent(aqlQuery, Encoding.UTF8, MediaTypeNames.Text.Plain); // AQL is sent as plain text

        var response = await _client.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode(); // Throw exception if API call failed

        return await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Uri CreateDownloadUri(IArtifactItem artifact)
    {
        // Ensure no double slashes and correct joining
        var baseUri = _client.BaseAddress ?? throw new InvalidOperationException("HttpClient BaseAddress is not set.");
        var repo = !string.IsNullOrWhiteSpace(artifact.Repo) ? artifact.Repo : Repository;

        if (string.IsNullOrWhiteSpace(artifact.Repo))
            artifact.Repo = Repository;

        // Construct relative path carefully
        var fullPath = !string.IsNullOrWhiteSpace(artifact.Name) ? $"{artifact.Path}/{artifact.Name}" : artifact.Path;
        var relativePath = $"{repo}{UriSeparator}{fullPath}".TrimStart(UriSeparator);

        return new Uri(baseUri, relativePath);
    }

    /// <inheritdoc />
    public IArtifactItem CreateArtifactItem(string path, string name, long? size = null, DateTime? modified = null)
        => new ArtifactItem { Repo = _repositoryKey, Path = path, Name = name, Size = size, Modified = modified };
}
