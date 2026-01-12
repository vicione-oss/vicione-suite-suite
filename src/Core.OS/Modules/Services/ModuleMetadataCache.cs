using Core.Module;
using Core.Module.Options;
using Core.OS.Modules.Extensions;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.Options;
using Sdk.Modules;
using Timer = System.Timers.Timer;

namespace Core.OS.Modules.Services;

// todo - make it possible to query a single metadata asset by name+version and load it?
public sealed class ModuleMetadataCache : IDisposable, IModuleMetadataCache
{
    public const string MetadataFileName = "module-metadata.json";
    public const string SetByEnvironmentMarker = "<set_by_environment>";

    private readonly IModuleArtifactRepository _moduleRepository;
    private readonly IModuleHost _moduleHost;
    private readonly IModuleOptionsStore _optionsStore;
    private readonly ILogger<ModuleMetadataCache> _logger;
    private List<ModuleMetadata>? _moduleMetadata;
    private Version? _sdkVersion;
    private readonly Timer _cacheInvalidationTimer;
    private readonly IConfiguration _environmentConfig;

    public ModuleMetadataCache(IModuleArtifactRepository moduleRepository,
        IOptions<ArtifactRepositoryOptions> options,
        IModuleHost moduleHost,
        IModuleOptionsStore optionsStore,
        ILogger<ModuleMetadataCache> logger)
    {
        _moduleRepository = moduleRepository;
        _moduleHost = moduleHost;
        _optionsStore = optionsStore;
        _logger = logger;

        _cacheInvalidationTimer = new Timer(options.Value.PackageCacheLifetimeMs) { AutoReset = false };
        _cacheInvalidationTimer.Elapsed += (_, _) =>
        {
            _moduleMetadata = null;
        };

        _environmentConfig = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>()
            .Build();
    }

    public async Task<List<ModuleMetadataBundle>> GetInstalledModuleMetadata(CancellationToken cancellationToken = default)
    {
        // load from suite sdk modules - suite dependency context? IFileSystem
        var installedModules = _moduleHost.GetManifestModules();

        foreach (var module in installedModules)
        {
            // now we have the metadata with the current option declarations
            // we can't mix up env, secrets here!!! we need to flag them later
            // !! options might change between requests so we need to enrich them again
            await EnrichOptionsWithCurrentValues(module.Metadata, cancellationToken);
        }

        return [.. installedModules];
    }

    private async Task EnrichOptionsWithCurrentValues(ModuleMetadata metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            // enrich the options with already stored ones
            // for now we only take options that are defined within metadata but custom ones need to be added soon
            if (metadata.Options is null || metadata.Options.Count == 0)
                return;

            // json options - we expect it exists
            var options = await _optionsStore.LoadJsonDictionary(metadata.Name, cancellationToken);

            foreach (var option in metadata.Options)
            {
                var optionKey = option.GetOptionKey(metadata.Name);

                // first check environment - it overrides json settings
                var envValue = _environmentConfig.GetValue<string?>(optionKey);
                if (!string.IsNullOrWhiteSpace(envValue))
                {
                    option.Value = SetByEnvironmentMarker;
                    continue;
                }

                // do we have matching key in json config?
                if (options.TryGetValue(optionKey, out var value))
                {
                    // we don't can take the default value of metadata here because it isn't part of the 
                    // effective configuration
                    option.Value = value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enrich metadata options for module '{Module}'", metadata.Name);
        }
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

        var assets = await _moduleRepository.QueryModuleMetadataArtifacts(sdkVersion, cancellationToken);
        if (assets.Count == 0)
        {
            _logger.LogWarning("No modules found for Sdk version {Version}", sdkVersion);
            return _moduleMetadata;
        }

        // try to download the metadata json from the asset url
        foreach (var asset in assets)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(asset);

                var metadata = await _moduleRepository.GetModuleMetadata(asset, cancellationToken)
                    ?? throw new InvalidOperationException($"Failed to extract metadata from repo '{asset.Repository}' item '{asset.Path}/{asset.Name}'");

                // this is not optimal because the metadata will be reduced to one later on...
                // but if we install a module that already has settings defined by environment
                // we have to mark them
                await EnrichOptionsWithCurrentValues(metadata, cancellationToken);

                _moduleMetadata.Add(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed read module metadata repo '{Repo}' item '{Path}/{Name}'", asset.Repository, asset.Path, asset.Name);
            }
        }

        // use same timer because one of the caches will disappear
        _cacheInvalidationTimer.Start();
        return _moduleMetadata;
    }

    public void Dispose() => _cacheInvalidationTimer.Dispose();
}
