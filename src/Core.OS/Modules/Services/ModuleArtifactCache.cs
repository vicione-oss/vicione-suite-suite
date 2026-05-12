using Core.Artifacts;
using Core.Module;
using Microsoft.Extensions.Options;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Timer = System.Timers.Timer;

namespace Core.OS.Modules.Services;

public sealed partial class ModuleArtifactCache : IDisposable, IModuleArtifactCache
{
    private readonly IModuleArtifactRepository _moduleRepository;
    private readonly ILogger<ModuleArtifactCache> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private List<ModuleMetadata>? _moduleMetadata;
    private Version? _sdkVersion;
    private readonly Timer _cacheInvalidationTimer;

    public ModuleArtifactCache(IModuleArtifactRepository moduleRepository,
        IOptions<ArtifactRepositoryOptions> options,
        ILogger<ModuleArtifactCache> logger)
    {
        _moduleRepository = moduleRepository;
        _logger = logger;

        _cacheInvalidationTimer = new Timer(options.Value.PackageCacheLifetimeMs);
        _cacheInvalidationTimer.Elapsed += async (_, _) =>
        {
            await Invalidate();
        };
    }

    public async Task<List<ModuleMetadata>> GetAvailableModuleMetadata(Version? sdkVersion = null, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_moduleMetadata is not null
                && !forceRefresh
                && sdkVersion == _sdkVersion)
                {
                    return _moduleMetadata;
                }

                _moduleMetadata = [];
                _sdkVersion = sdkVersion;

                // todo: maybe it's better to request only the available versions per installed module here
                // more requests but less data...
                var assets = await _moduleRepository.QueryModuleMetadataArtifacts(sdkVersion, null, cancellationToken);
                if (assets.Count == 0)
                {
                    LogNoModulesFound(_logger, sdkVersion);
                    return _moduleMetadata;
                }

                // try to download the metadata json from the asset url
                var metadataTasks = assets.Select(async asset => await TryGetModuleMetadata(_moduleRepository, asset, _logger, cancellationToken));

                var metadataResults = await Task.WhenAll(metadataTasks);
                _moduleMetadata.AddRange(metadataResults.Where(metadata => metadata is not null)!);

                // use same timer because one of the caches will disappear
                _cacheInvalidationTimer.Start();
                return _moduleMetadata;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or other object already disposed, nothing we can do, return gracefully
        }

        return [];
    }

    private static async Task<ModuleMetadata?> TryGetModuleMetadata(IModuleArtifactRepository repository, IArtifact asset, ILogger<ModuleArtifactCache> logger, CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(asset);

            return await repository.GetModuleMetadata(asset, cancellationToken)
                ?? throw new InvalidOperationException($"Failed to extract metadata from repo '{asset.Repository}' item '{asset.Path}/{asset.Name}'");
        }
        catch (OperationCanceledException)
        {
            // ignore this one, we don't want to log errors on cancellation
            return null;
        }
        catch (Exception ex)
        {
            LogReadingModuleMetadata(logger, ex, asset.Repository, asset.Path, asset.Name);
            return null;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _cacheInvalidationTimer.Dispose();
        _semaphore.Dispose();
    }

    public async Task Invalidate(CancellationToken cancellationToken = default)
    {
        try
        {
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);

            await _semaphore.WaitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
            try { _moduleMetadata = null; }
            finally { _semaphore.Release(); }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException) { }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No modules found for Sdk version '{Version}'")]
    private static partial void LogNoModulesFound(ILogger<ModuleArtifactCache> logger, Version? version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed reading module metadata repository '{Repo}' item '{Path}/{Name}'")]
    private static partial void LogReadingModuleMetadata(ILogger<ModuleArtifactCache> logger, Exception ex, string repo, string path, string name);
}
