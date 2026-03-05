using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Requests;
using Sdk.Modules;

namespace Blazor.Shared.Module.Services;

public interface IModuleManagementService
{
    Task<GetModuleMetadataBundlesResponse> GetModuleMetadata(bool forceReload = false, CancellationToken cancellationToken = default);

    Task SendUpdateModulePackages(List<ModulePackageOperation> operations, CancellationToken cancellationToken = default);

    Task SendUpdateModuleOptions(string moduleId, IEnumerable<ModuleOptionDeclaration> options, CancellationToken cancellationToken = default);
}
