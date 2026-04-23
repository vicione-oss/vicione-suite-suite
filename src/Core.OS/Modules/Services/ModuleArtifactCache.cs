using Core.Artifacts;
using Core.Module;
using Core.Module.Options;
using Microsoft.Extensions.Options;
using Sdk.Modules;
using Timer = System.Timers.Timer;

namespace Core.OS.Modules.Services;

public sealed class ModuleArtifactCache : IDisposable, IModuleArtifactCache
{
    private readonly IModuleArtifactRepository _moduleRepository;
    private readonly ILogger<ModuleArtifactCache> _logger;
    private List<ModuleMetadata>? _moduleMetadata;
    private Version? _sdkVersion;
    private readonly Timer _cacheInvalidationTimer;

    public ModuleArtifactCache(IModuleArtifactRepository moduleRepository,
        IOptions<ArtifactRepositoryOptions> options,
        ILogger<ModuleArtifactCache> logger)
    {
        _moduleRepository = moduleRepository;
        _logger = logger;

        _cacheInvalidationTimer = new Timer(options.Value.PackageCacheLifetimeMs) { AutoReset = false };
        _cacheInvalidationTimer.Elapsed += (_, _) =>
        {
            _moduleMetadata = null;
        };
    }

    public async Task<List<ModuleMetadata>> GetAvailableModuleMetadata(Version? sdkVersion = null, bool forceRefresh = false, CancellationToken cancellationToken = default)
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
            _logger.LogWarning("No modules found for Sdk version {Version}", sdkVersion);
            return _moduleMetadata;
        }

        // try to download the metadata json from the asset url
        var metadataTasks = assets.Select(async asset =>
        {
            try
            {
                ArgumentNullException.ThrowIfNull(asset);

                return await _moduleRepository.GetModuleMetadata(asset, cancellationToken)
                    ?? throw new InvalidOperationException($"Failed to extract metadata from repo '{asset.Repository}' item '{asset.Path}/{asset.Name}'");
            }
            catch (OperationCanceledException)
            {
                // ignore this one, we don't want to log errors on cancellation
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed read module metadata repo '{Repo}' item '{Path}/{Name}'", asset.Repository, asset.Path, asset.Name);
                return null;
            }
        });

        var metadataResults = await Task.WhenAll(metadataTasks);
        _moduleMetadata.AddRange(metadataResults.Where(metadata => metadata is not null)!);

        // use same timer because one of the caches will disappear
        _cacheInvalidationTimer.Start();
        return _moduleMetadata;
    }

    public void Dispose() => _cacheInvalidationTimer.Dispose();

    public void Invalidate() => _moduleMetadata = null;
}
