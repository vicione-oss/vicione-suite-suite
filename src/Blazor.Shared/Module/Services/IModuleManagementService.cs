using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Requests;
using Sdk.Modules;

namespace Blazor.Shared.Module.Services;

internal interface IModuleManagementService
{
    string GetSdkVersion();

    Task<GetModuleMetadataBundlesResponse> GetModuleMetadata(bool addPreReleases = false, bool forceReload = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a module manifest out of the metadata models and sends it to the backend. 
    /// If options were modified they will be sent to the backend too
    /// </summary>
    /// <param name="models"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UpdateModulePackageVersions(List<ModuleMetadataModel> models, CancellationToken cancellationToken = default);

    Task UpdateModuleOptions(string moduleId, IEnumerable<ModuleOptionDeclaration> options, CancellationToken cancellationToken = default);
}
