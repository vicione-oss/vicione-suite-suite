using System.IO.Abstractions;
using System.IO.Compression;
using System.Net.Http.Json;
using Core.Module.Contracts;
using Core.Module.Extensions;
using Core.Module.Nexus.Contracts;
using Core.Module.Options;
using Microsoft.Extensions.Options;
using Sdk.Backend.ArtifactApi;

namespace Core.Module.Nexus;

public class NexusArtifactQueryApi : IArtifactQueryApi
{
    private const string AssetApiPart = "assets";
    private const string NexusModulesFolder = "modules";
    private readonly string[] _searchApiParts = [
        "service",
        "rest",
        "v1",
        "search"
    ];

    private readonly ModuleApiOptions _apiOptions;
    private readonly HttpClient _client;
    private readonly IFileSystem _fileSystem;

    private NexusApiQueryBuilder? _nexusApiQueryBuilder;

    /// <summary>
    /// Can't use IFileSystem here because of used ZipFileExtensions
    /// </summary>
    public NexusArtifactQueryApi(IFileSystem fileSystem, IOptions<ModuleApiOptions> options, HttpClient client)
    {
        _fileSystem = fileSystem;
        _client = client;
        _apiOptions = options.Value;

        _client.AddDefaultRequestHeaders(_apiOptions);
    }

    public Uri CreateDownloadUri(IArtifactItem artifact)
    {
        var uriBuilder = new UriBuilder(_apiOptions.Endpoint);
        uriBuilder.Path = Path.Combine(
        [
            uriBuilder.Path,
            NexusModulesFolder,
            artifact.Path,
            artifact.Name,
        ]);

        return uriBuilder.Uri;
    }

    public IArtifactQueryBuilder CreateQueryBuilder()
    {
        // we need this to do acccess the sorting options later on
        // because nexus does not support it
        _nexusApiQueryBuilder = new NexusApiQueryBuilder();

        return _nexusApiQueryBuilder;
    }

    public async Task DownloadAndExtract(IArtifactItem artifact, string targetFolderPath, CancellationToken cancellationToken = default)
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

    public Task<Stream> DownloadStream(IArtifactItem artifact, CancellationToken cancellationToken = default)
    {
        var sourceUri = CreateDownloadUri(artifact);

        return _client.GetStreamAsync(sourceUri, cancellationToken);
    }

    public async Task DownloadToFile(IArtifactItem artifact, string targetFilePath, CancellationToken cancellationToken = default)
    {
        await using var httpStream = await DownloadStream(artifact, cancellationToken);
        await using var fs = _fileSystem.FileStream.New(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);

        await httpStream.CopyToAsync(fs, cancellationToken);
    }

    public async Task<IArtifactQueryResult> ExecuteQuery(string aqlQuery, CancellationToken cancellationToken = default)
    {
        try
        {
            var apiPath = GetAssetApiPath();

            // we get back assets for sdk version but might get pre-releases too - need to filter them out        
            var assets = await QueryContinuousApi<SearchAssetsResponse>(apiPath, aqlQuery, cancellationToken);
            var result = new ArtifactQueryResult()
            {
                Results = [.. assets.SelectMany(k => k.Items).Select(CreateArtifact)]
            };

            return result;
        }
        catch (Exception ex)
        {
            return new ArtifactQueryResult() { ErrorInfo = new Sdk.Messaging.ErrorInfo(100, ex.Message) };
        }

        static ArtifactItem CreateArtifact(SearchResponseAsset asset)
        {
            var name = Path.GetFileName(asset.Path);
            var path = Path.GetDirectoryName(asset.Path) ?? string.Empty;

            return new ArtifactItem
            {
                Name = name,
                Path = path,
                Modified = asset.LastModified,
                Repo = asset.Path,
                Checksum = asset.CheckSum == null ? null : new ArtifactChecksum
                {
                    Sha1 = asset.CheckSum.Sha1,
                    Md5 = asset.CheckSum.Md5,
                    Sha256 = asset.CheckSum.Sha256,
                    Sha512 = asset.CheckSum.Sha512
                }
            };
        }
    }

    private async Task<List<T>> QueryContinuousApi<T>(string apiPath, string? queryString, CancellationToken cancellationToken = default)
        where T : IContinuationResponse
    {
        // https://nexus.nsc-gmbh.de/service/rest/v1/search/assets?repository=raw-fb-server&name=/modules*
        var uriBuilder = new UriBuilder(_apiOptions.Endpoint);
        var repository = Path.GetFileName(uriBuilder.Path);

        uriBuilder.Path = apiPath;

        var responses = new List<T>();
        string? continuationToken = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            // https://help.sonatype.com/en/pagination.html
            uriBuilder.Query = ExtendSearchQuery(queryString, repository, continuationToken);

            var response = await _client.GetFromJsonAsync<T>(uriBuilder.ToString(), cancellationToken);
            if (response is null)
                break;

            responses.Add(response);

            if (string.IsNullOrWhiteSpace(response.ContinuationToken))
                break;

            continuationToken = response.ContinuationToken;
        }

        return responses;
    }

    private static string ExtendSearchQuery(string? query, string repository, string? continuationToken)
    {
        var queryParams = new List<KeyValuePair<string, string>>()
        {
            CreateRepositoryParam(repository),
        };

        if (!string.IsNullOrEmpty(continuationToken))
            queryParams.Add(CreateContinuationTokenParam(continuationToken));

        var queryExtensions = string.Join("&", queryParams.Select(k => $"{k.Key}={k.Value}"));

        if (string.IsNullOrEmpty(query))
            return queryExtensions;

        // if we get more query stuff to handle we should replace it with
        // https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.querystring.create?view=aspnetcore-8.0
        return $"{query}&{queryExtensions}";
    }

    private static KeyValuePair<string, string> CreateRepositoryParam(string repository)
        => new("repository", repository);


    private static KeyValuePair<string, string> CreateContinuationTokenParam(string continuationToken)
        => new("continuationToken", continuationToken);

    private string GetAssetApiPath()
        => Path.Combine([.. _searchApiParts, AssetApiPart]);

    public IArtifactItem CreateArtifactItem(string path, string name, long? size = null, DateTime? modified = null)
        => new ArtifactItem { Repo = "Todo", Path = path, Name = name, Size = size, Modified = modified };
}
