using Core.OS.Modules.Contracts;
using Core.Shared.Modules.Contracts;

namespace Core.OS.Modules;

/// <summary>
/// Provides metadata for installed or available module packages
/// </summary>
public interface IModuleMetadataProvider
{
    /// <summary>
    /// Returns metadata for currently installed modules. The metadata is enriched with current option values. 
    /// The cache is automatically invalidated after the configured lifetime has elapsed, ensuring that the returned metadata is up-to-date.
    /// </summary>
    Task<List<ModuleMetadataBundle>> GetModuleMetadata(GetModuleMetadataOptions options, CancellationToken cancellationToken = default);
}
