using Sdk.Modules;

namespace Core.OS.Modules;

/// <summary>
/// Defines methods for asynchronously retrieving metadata about available modules.
/// </summary>
public interface IModuleArtifactCache
{
    /// <summary>
    /// Returns metadata for available modules in the repository for the current SDK version.
    /// When <paramref name="forceRefresh"/> is <see langword="true"/>, the in-memory cache is fully reset
    /// and all artifacts are re-fetched from the repository.
    /// </summary>
    Task<List<ModuleMetadata>> GetAvailableModuleMetadata(bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the cache so the next call to <see cref="GetAvailableModuleMetadata"/> performs a full re-fetch from
    /// the module repository. Use this to proactively refresh the cache when changes to repositories are made.
    /// </summary>
    Task Invalidate(CancellationToken cancellationToken = default);
}
