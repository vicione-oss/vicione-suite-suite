using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Requests;
using Sdk.Client.Infrastructure;
using Sdk.Modules;

namespace Blazor.Shared.Module.Services;

internal sealed class ModuleManagementService(IUiMediator mediator) : IModuleManagementService
{
    public async Task<GetModuleMetadataBundlesResponse> GetModuleMetadata(bool forceReload = false, CancellationToken cancellationToken = default)
    {
        var request = new GetModuleMetadataBundlesRequest(true, true, forceReload);
        return await mediator.Request<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>(request, cancellationToken);
    }

    public async Task SendUpdateModuleOptions(string moduleId, IEnumerable<ModuleOptionDeclaration> options, CancellationToken cancellationToken = default)
    {
        var command = new UpdateModuleOptions(moduleId, options.Where(k => k.Value != Core.Shared.Constants.SetByEnvironmentMarker).ToList());
        await mediator.Send(command, cancellationToken);
    }

    public async Task SendUpdateModulePackages(List<ModulePackageOperation> operations, CancellationToken cancellationToken = default)
    {
        var command = new UpdateModulePackageOperations(operations);
        await mediator.Send(command, cancellationToken);
    }
}
