using System.IO.Abstractions;
using System.Text.Json;
using Core.Module;
using Core.Module.Exceptions;
using Core.Module.Utils;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Semver;

namespace Core.OS.Modules.Services;

public sealed partial class ModuleArtifactCache : IDisposable, IModuleArtifactCache
{
    private const int CacheSchemaVersion = 1;
    private const int MaxConcurrentMetadataRequests = 20;
    internal const string CacheFileName = "modules-cache.json";

    // Re-query a small window before the last fetch so artifacts that share the boundary timestamp
    // (or arrive slightly out of order across sources) are not skipped. Already cached items are
    // de-duplicated by identity so the overlap only costs a cheap query, no extra downloads.
    private static readonly TimeSpan WatermarkOverlap = TimeSpan.FromMinutes(1);

    private readonly IModuleArtifactRepository _moduleRepository;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ModuleArtifactCache> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly string _cacheFilePath;
    private readonly Version _sdkVersion;

    // Keyed by artifact identity (SourceKey/Path/Name). Module artifacts are immutable so an identity
    // never changes; only new identities are added over time.
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private HashSet<string>? _sourceKeys;
    private DateTimeOffset? _watermark;
    private bool _loaded;

    public ModuleArtifactCache(IModuleArtifactRepository moduleRepository,
        IFileSystem fileSystem,
        IWorkspaceManagement workspaceManagement,
        ILogger<ModuleArtifactCache> logger)
    {
        _moduleRepository = moduleRepository;
        _fileSystem = fileSystem;
        _logger = logger;
        _cacheFilePath = _fileSystem.Path.Combine(workspaceManagement.GetCacheDirectory(Sdk.Constants.SystemModuleId), CacheFileName);
        _sdkVersion = ModuleHelpers.GetSdkAssemblyVersion();
    }

    public async Task<List<ModuleMetadata>> GetAvailableModuleMetadata(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        try
        {
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);

            // SemaphoreSlim has 20 slots. The first 20 tasks acquire a slot and proceed to the HTTP call.
            // Tasks 21+ suspend here — they are parked in the semaphore's internal wait queue,
            // consuming no thread and making no HTTP request.
            await _semaphore.WaitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);

            try
            {
                // Seed from the persisted file once so we can resume from the last fetch instead of
                // querying all sources from scratch.
                await EnsureLoadedAsync(linkedCancellationTokenSource.Token);

                // A change of the source set invalidates everything because items of a removed source
                // must disappear and a new source might bring overlapping identities.
                var currentSources = GetCurrentSourceKeys();
                if (_sourceKeys is null || !_sourceKeys.SetEquals(currentSources))
                {
                    ResetCache();
                    _sourceKeys = currentSources;
                }

                // A forced refresh re-reads everything; the timed gate only fetches newly added artifacts.
                if (forceRefresh)
                    ResetCache();

                await RefreshAsync(linkedCancellationTokenSource.Token);

                return ProjectMetadata();
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

    public async Task Invalidate(CancellationToken cancellationToken = default)
    {
        try
        {
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);

            await _semaphore.WaitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                ResetCache();
                _sourceKeys = null;
                DeletePersistedCache();
            }
            finally { _semaphore.Release(); }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException) { }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // Only request artifacts added after the last successful fetch. The SDK filter is applied in
        // memory on read so we can cache the broad result independent of the requested SDK version.
        var assets = await _moduleRepository.QueryModuleMetadataArtifacts(_sdkVersion, _watermark, cancellationToken);
        if (assets.Count == 0)
        {
            if (_watermark is null)
                LogNoModulesFound(_logger);

            return;
        }

        // Download metadata only for artifacts we have not seen yet, this is the expensive part.
        var newAssets = assets.Where(asset => !_entries.ContainsKey(GetKey(asset))).ToList();
        CacheEntry?[] entries = [];

        if (newAssets.Count > 0)
        {
            entries = new CacheEntry?[newAssets.Count];

            // Use a sliding window instead of Task.WhenAll to cap concurrent HTTP requests.
            // Tasks queued beyond the concurrency limit cancel fast via WaitAsync before issuing any HTTP call.
            using var throttle = new SemaphoreSlim(MaxConcurrentMetadataRequests, MaxConcurrentMetadataRequests);
            var tasks = newAssets.Select((asset, i) => ThrottledGetEntry(i, asset, throttle, cancellationToken)).ToArray();
            await Task.WhenAll(tasks);

            async Task ThrottledGetEntry(int index, IArtifact asset, SemaphoreSlim semaphore, CancellationToken ct)
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    entries[index] = await TryGetEntry(_moduleRepository, asset, _logger, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }

        var changed = false;
        for (var i = 0; i < newAssets.Count; i++)
        {
            if (entries[i] is { } entry)
            {
                _entries[GetKey(newAssets[i])] = entry;
                changed = true;
            }
        }

        var newWatermark = ComputeWatermark(assets, newAssets, entries);
        if (newWatermark != _watermark)
        {
            _watermark = newWatermark;
            changed = true;
        }

        if (changed)
            await PersistAsync(cancellationToken);
    }

    private List<ModuleMetadata> ProjectMetadata()
    {
        if (_entries.Count == 0)
            return [];

        var result = new List<ModuleMetadata>();
        foreach (var artifact in _entries.Values.Select(entry => entry.Artifact))
        {
            if (_entries.TryGetValue(GetKey(artifact), out var entry))
                result.Add(entry.Metadata);
        }

        return result;
    }

    private DateTimeOffset? ComputeWatermark(IReadOnlyList<IArtifact> assets, List<IArtifact> attempted, IReadOnlyList<CacheEntry?> entries)
    {
        DateTimeOffset? max = null;
        foreach (var asset in assets)
        {
            if (asset.Modified is { } modified && (max is null || modified > max))
                max = modified;
        }

        // No usable timestamps, keep querying broadly to avoid skipping artifacts.
        if (max is null)
            return _watermark;

        // Never advance past an artifact whose metadata download failed so it is retried next time.
        DateTimeOffset? earliestFailure = null;
        for (var i = 0; i < attempted.Count; i++)
        {
            if (entries[i] is null && attempted[i].Modified is { } modified && (earliestFailure is null || modified < earliestFailure))
                earliestFailure = modified;
        }

        var candidate = earliestFailure is { } failure && failure < max ? failure : max.Value;
        return candidate - WatermarkOverlap;
    }

    private void ResetCache()
    {
        _entries.Clear();
        _watermark = null;
    }

    private HashSet<string> GetCurrentSourceKeys()
    {
        try
        {
            return [.. _moduleRepository.GetSourceKeys()];
        }
        catch (Exception ex)
        {
            // On failure keep the current snapshot to avoid a spurious full invalidation.
            LogFailedToGetSourceKeys(_logger, ex);
            return _sourceKeys is null ? [] : [.. _sourceKeys];
        }
    }

    private static string GetKey(IArtifact artifact) => $"{artifact.SourceKey}/{artifact.Path}/{artifact.Name}";

    private static async Task<CacheEntry?> TryGetEntry(IModuleArtifactRepository repository, IArtifact asset, ILogger<ModuleArtifactCache> logger, CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(asset);

            var metadata = await repository.GetModuleMetadata(asset, cancellationToken)
                ?? throw new InvalidOperationException($"Failed to extract metadata from repo '{asset.Repository}' item '{asset.Path}/{asset.Name}'");

            LogRetrievedMetadata(logger, asset.Repository, asset.Path, asset.Name);

            return new CacheEntry(asset, metadata);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not logged as an error.
            return null;
        }
        catch (Exception ex)
        {
            LogReadingModuleMetadata(logger, ex, asset.Repository, asset.Path, asset.Name);
            return null;
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
            return;

        _loaded = true;

        if (!_fileSystem.File.Exists(_cacheFilePath))
        {
            // No persisted cache, start fresh.
            ResetState();
            return;
        }

        try
        {
            CacheFile? data;
            await using (var stream = _fileSystem.FileStream.New(_cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                data = await JsonSerializer.DeserializeAsync<CacheFile>(stream, ModuleSerializerOptions.GetOptions(), cancellationToken);
            }

            if (data is null || data.Schema != CacheSchemaVersion)
                return;

            // Discard the persisted cache when it was produced by a different SDK. A changed SDK can alter
            // module compatibility and metadata semantics, so a full re-fetch is safer than the stale snapshot.
            if (!IsSdkCompatible(data.SdkVersion))
            {
                LogDiscardedPersistedCacheSdkVersion(_logger, data.SdkVersion, _sdkVersion);
                return;
            }

            // Discard the persisted cache when the sources changed while the service was offline.
            var currentSources = GetCurrentSourceKeys();
            if (!currentSources.SetEquals(data.SourceKeys))
            {
                LogDiscardedPersistedCache(_logger);
                return;
            }

            foreach (var entry in data.Entries)
            {
                var artifact = new CachedArtifact(entry.SourceKey, entry.Path, entry.Name, entry.Modified);
                _entries[GetKey(artifact)] = new CacheEntry(artifact, entry.Metadata);
            }

            _watermark = data.Watermark;
            _sourceKeys = currentSources;

            LogLoadedPersistedCache(_logger, _entries.Count, _watermark);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailedToLoadPersistedCache(_logger, ex, _cacheFilePath);

            ResetState();
        }
        return;

        void ResetState()
        {
            _entries.Clear();
            _watermark = null;
            _sourceKeys = null;
        }
    }

    private bool IsSdkCompatible(string cacheSdkVersion)
    {
        try
        {
            var sdkVersion = SemVersion.FromVersion(_sdkVersion);
            ModuleVersionValidator.ValidateSdkCompatibility(sdkVersion, cacheSdkVersion);
        }
        catch (SdkIncompatibilityException)
        {
            return false;
        }

        return true;
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = new CacheFile
            {
                Schema = CacheSchemaVersion,
                SdkVersion = ModuleHelpers.GetNormalizedVersion(_sdkVersion),
                SourceKeys = [.. _sourceKeys ?? []],
                Watermark = _watermark,
                Entries = [.. _entries.Values.Select(entry => new CacheFileEntry
                {
                    SourceKey = entry.Artifact.SourceKey,
                    Path = entry.Artifact.Path,
                    Name = entry.Artifact.Name,
                    Modified = entry.Artifact.Modified,
                    Metadata = entry.Metadata,
                })],
            };

            var directory = _fileSystem.Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(directory) && !_fileSystem.Directory.Exists(directory))
                _fileSystem.Directory.CreateDirectory(directory);

            // Write to a temp file first and move it so a crash can't leave a corrupt cache file.
            var tempPath = _cacheFilePath + ".tmp";

            if (_fileSystem.File.Exists(tempPath))
                _fileSystem.File.Delete(tempPath);

            await using (var stream = _fileSystem.FileStream.New(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, data, ModuleSerializerOptions.GetOptions(), cancellationToken);
            }

            _fileSystem.File.Move(tempPath, _cacheFilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailedToPersistCache(_logger, ex, _cacheFilePath);
        }
    }

    private void DeletePersistedCache()
    {
        try
        {
            if (_fileSystem.File.Exists(_cacheFilePath))
                _fileSystem.File.Delete(_cacheFilePath);
        }
        catch (Exception ex)
        {
            LogFailedToDeletePersistedCache(_logger, ex, _cacheFilePath);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _semaphore.Dispose();
    }

    private sealed record CacheEntry(IArtifact Artifact, ModuleMetadata Metadata);

    /// <summary>
    /// Lightweight <see cref="IArtifact"/> used to rehydrate entries loaded from the persisted cache.
    /// Only the identity and modification timestamp are relevant for caching and SDK filtering.
    /// </summary>
    private sealed class CachedArtifact(string sourceKey, string path, string name, DateTimeOffset? modified) : IArtifact
    {
        public DateTimeOffset? Modified { get; } = modified;
        public string Name { get; } = name;
        public string Path { get; } = path;
        public string Repository { get; } = string.Empty;
        public long? Size => null;
        public ArtifactKind Kind => ArtifactKind.File;
        public string SourceKey { get; } = sourceKey;
        public IArtifactChecksum? Checksum => null;
    }

    private sealed class CacheFile
    {
        public int Schema { get; init; } = CacheSchemaVersion;
        public string SdkVersion { get; init; } = string.Empty;
        public List<string> SourceKeys { get; init; } = [];
        public DateTimeOffset? Watermark { get; init; }
        public List<CacheFileEntry> Entries { get; init; } = [];
    }

    private sealed class CacheFileEntry
    {
        public required string SourceKey { get; init; }
        public required string Path { get; init; }
        public required string Name { get; init; }
        public DateTimeOffset? Modified { get; init; }
        public required ModuleMetadata Metadata { get; init; }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No module metadata artifacts found in repository")]
    private static partial void LogNoModulesFound(ILogger<ModuleArtifactCache> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed reading module metadata repository '{Repository}' item '{Path}/{Name}'")]
    private static partial void LogReadingModuleMetadata(ILogger<ModuleArtifactCache> logger, Exception ex, string repository, string path, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get artifact source keys")]
    private static partial void LogFailedToGetSourceKeys(ILogger<ModuleArtifactCache> logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Requested module metadata artifact '{Repository}' item '{Path}/{Name}'")]
    private static partial void LogRetrievedMetadata(ILogger<ModuleArtifactCache> logger, string repository, string path, string name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} cached module metadata entries from file, last fetch '{Watermark}'")]
    private static partial void LogLoadedPersistedCache(ILogger<ModuleArtifactCache> logger, int count, DateTimeOffset? watermark);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discarded persisted module metadata cache because the artifact sources changed")]
    private static partial void LogDiscardedPersistedCache(ILogger<ModuleArtifactCache> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discarded persisted module metadata cache because the SDK version changed from '{OldVersion}' to '{NewVersion}'")]
    private static partial void LogDiscardedPersistedCacheSdkVersion(ILogger<ModuleArtifactCache> logger, string oldVersion, Version newVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load persisted module metadata cache from '{Path}'")]
    private static partial void LogFailedToLoadPersistedCache(ILogger<ModuleArtifactCache> logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to persist module metadata cache to '{Path}'")]
    private static partial void LogFailedToPersistCache(ILogger<ModuleArtifactCache> logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete persisted module metadata cache at '{Path}'")]
    private static partial void LogFailedToDeletePersistedCache(ILogger<ModuleArtifactCache> logger, Exception ex, string path);
}
