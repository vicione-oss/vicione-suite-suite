using System.IO.Abstractions;
using System.Text.Json;
using Core.Artifacts;
using Core.Module.Options;
using Core.OS.Instance.Extensions;
using Core.Shared.Instance.Contracts;
using Microsoft.Extensions.Options;
using Sdk.Messaging;

namespace Core.OS.Instance.Services;

internal partial class ArtifactRepositoryStore(IFileSystem fileSystem, IOptions<InstanceOptions> instanceOptions, ILogger logger) : IArtifactRepositoryStore, IDisposable
{
    private const string RepositoriesFileName = "repo-sources.json";
    private const string MigrationFallbackTokenEndpoint = "https://system.update.ifm/artifactory/vicione-token/token.json";

    private readonly SemaphoreSlim _enqueueLock = new(1, 1);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _enqueueLock.Dispose();
    }

    public async Task MigrateConfiguredRepositories(IConfiguration config, CancellationToken cancellationToken = default)
    {
        var repositories = await GetRepositories(null, cancellationToken);
        if (repositories.Count > 0)
            return;

        var envOptions = GetEnvironmentOptions(config);
        var repoSources = envOptions.Sources.Select(s => new ArtifactRepository
        {
            Id = Guid.NewGuid(),
            Name = TryGuessEndpointName(s.Endpoint),
            Enabled = true, // enabled env settings by default
            Endpoint = s.Endpoint,
            UserName = s.UserName,
            Password = s.Password,
            TokenEndpoint = s.TokenEndpoint ?? MigrationFallbackTokenEndpoint,
            Modified = DateTimeOffset.UtcNow,
            ModifiedBy = Environment.UserName,
        }).ToList();

        await SerializeToFile(repoSources, fileSystem, instanceOptions.Value, cancellationToken);

        static string TryGuessEndpointName(string endpoint)
        {
            try
            {
                var uri = new Uri(endpoint);
                return uri.Segments.Last();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public async Task<List<ArtifactRepository>> GetRepositories(IReadOnlyCollection<Guid>? repositoryIds, CancellationToken cancellationToken = default)
    {
        var repositories = await DeserializeRepositories(fileSystem, instanceOptions.Value, cancellationToken);

        return repositoryIds is null || repositoryIds.Count == 0
            ? repositories
            : [.. repositories.Where(s => repositoryIds.Contains(s.Id))];
    }

    public async Task<CrudAction> CreateOrUpdate(ArtifactRepository source, CancellationToken cancellationToken = default)
    {
        var repositories = await DeserializeRepositories(fileSystem, instanceOptions.Value, cancellationToken);

        var exists = repositories.FirstOrDefault(s => s.Id == source.Id);
        if (exists is not null)
        {
            exists.UserName = source.UserName;
            exists.Password = source.Password;
            exists.Name = source.Name;
            exists.Enabled = source.Enabled;
            exists.Endpoint = source.Endpoint;
            exists.Modified = source.Modified;
            exists.ModifiedBy = source.ModifiedBy;
            exists.TokenEndpoint = source.TokenEndpoint;
            exists.TokenValidUntil = source.TokenValidUntil;
        }
        else
        {
            repositories.Add(source);
        }

        await SerializeToFile(repositories, fileSystem, instanceOptions.Value, cancellationToken);

        return exists is null ? CrudAction.Created : CrudAction.Updated;
    }

    public async Task<IReadOnlyCollection<ArtifactRepository>> Delete(IReadOnlyCollection<Guid> repositoryIds, CancellationToken cancellationToken = default)
    {
        var sources = await DeserializeRepositories(fileSystem, instanceOptions.Value, cancellationToken);
        var toDelete = sources.Where(s => repositoryIds.Contains(s.Id)).ToList();

        foreach (var source in toDelete)
            sources.Remove(source);

        await SerializeToFile(sources, fileSystem, instanceOptions.Value, cancellationToken);

        return toDelete;
    }


    private static string GetRepositoriesFilePath(IFileSystem fs, InstanceOptions options)
        => fs.Path.Combine(fs.GetRootedHomeDirectory(options), RepositoriesFileName);

    private static async Task<List<ArtifactRepository>> DeserializeRepositories(IFileSystem fs, InstanceOptions options, CancellationToken cancellationToken)
    {
        var updateFile = GetRepositoriesFilePath(fs, options);
        if (!fs.File.Exists(updateFile))
            return [];

        await using var updateFileStream = fs.FileStream.New(updateFile, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });

        return await JsonSerializer.DeserializeAsync<List<ArtifactRepository>>(updateFileStream, DefaultJsonSerializerSettings.Default, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize repositories from '{updateFile}'");
    }


    private async Task SerializeToFile(List<ArtifactRepository> repositories, IFileSystem fs, InstanceOptions options, CancellationToken cancellationToken)
    {
        await _enqueueLock.WaitAsync(cancellationToken);
        try
        {
            var repositoriesFilePath = GetRepositoriesFilePath(fs, options);

            // Rewrite file containing repositories
            await using var fileStream = fs.FileStream.New(repositoriesFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fileStream, repositories, DefaultJsonSerializerSettings.Default, cancellationToken);
        }
        finally
        {
            _enqueueLock.Release();
        }
    }

    private static ArtifactRepositoryOptions GetEnvironmentOptions(IConfiguration config)
    {
        var options = config.GetSection(ArtifactRepositoryOptions.ConfigSection).Get<ArtifactRepositoryOptions>()
            ?? new ArtifactRepositoryOptions();

        // ModuleApiOptions will get removed once but to stay backwards compatible we
        // need to integrate it into the new ArtifactRepositoryOptions
#pragma warning disable CS0618 // Obsolete class usage
        var obsoleteOptions = config.GetSection(ModuleApiOptions.ConfigSection).Get<ModuleApiOptions>();
#pragma warning restore CS0618 // Obsolete class usage

        if (obsoleteOptions is not null)
        {
            if (options.Sources.All(k => k.Endpoint != obsoleteOptions.Endpoint))
            {
                options.Sources.Add(new ArtifactRepositorySourceOption
                {
                    Endpoint = obsoleteOptions.Endpoint,
                    UserName = obsoleteOptions.UserName,
                    Password = obsoleteOptions.Password,
                });
            }
        }

        return options;
    }

    public Task Store(List<ArtifactRepository> repositories, CancellationToken cancellationToken = default)
        => SerializeToFile(repositories, fileSystem, instanceOptions.Value, cancellationToken);

    public Task Clear(CancellationToken cancellationToken = default)
    {
        var updateFile = GetRepositoriesFilePath(fileSystem, instanceOptions.Value);
        if (!fileSystem.File.Exists(updateFile))
            return Task.CompletedTask;

        LogClearArtifactSources(logger);

        fileSystem.File.Delete(updateFile);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Debug, "Clear artifact sources")]
    private static partial void LogClearArtifactSources(ILogger logger);
}
