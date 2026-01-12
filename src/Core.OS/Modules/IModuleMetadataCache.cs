using Core.Shared.Modules.Contracts;
using Sdk.Modules;

namespace Core.OS.Modules;

public interface IModuleMetadataCache
{
    Task<List<ModuleMetadataBundle>> GetInstalledModuleMetadata(CancellationToken cancellationToken = default);
    Task<List<ModuleMetadata>> GetAvailableModuleMetadata(Version? sdkVersion = null, bool includePreReleases = false, bool forceRefresh = false, CancellationToken cancellationToken = default);
}
