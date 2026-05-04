using Sdk.Modules;

namespace Core.OS.Modules;

/// <summary>
/// Defines methods for asynchronously retrieving metadata about installed and available modules. Metadata loaded from this cache is enriched with current option values.
/// Cache lifetime can be configured via <see cref="ArtifactRepositoryOptions.PackageCacheLifetimeMs"/> and is automatically invalidated after the configured time has elapsed.
/// </summary>
public interface IModuleArtifactCache
{
    /// <summary>
    /// Returns metadata for available modules in the repository, optionally filtered by SDK version. If <c>forceRefresh</c> is set to false, the method may return cached metadata if it is still valid; 
    /// otherwise, it retrieves fresh metadata from the module repository.
    /// </summary>    
    Task<List<ModuleMetadata>> GetAvailableModuleMetadata(Version? sdkVersion = null, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate the cache so next call to <see cref="GetAvailableModuleMetadata"/> will fetch fresh metadata from the module repository. 
    /// This can be used to proactively refresh the cache when changes to repositories are made, 
    /// instead of waiting for the automatic invalidation to occur.
    /// </summary>
    Task Invalidate(CancellationToken cancellationToken = default);
}
