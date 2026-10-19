using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.Models;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Events;
using Core.Shared.Modules.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Modules;

namespace Blazor.Shared.Module.Services;

internal sealed class ModuleManagementService : CompletionSourceHandlerBase<IModuleManagementServiceResult>,
    IModuleManagementService,
    IEventConsumer<ModulePackageOperationsChanged>,
    IEventConsumer<ModuleOptionsChanged>
{
    public event Func<ModulePackageOperationsChanged, CancellationToken, Task>? OperationsChanged;

    public event Func<ModuleOptionsChanged, CancellationToken, Task>? OptionsChanged;

    public ModuleManagementService(IUiMediator mediator) : base(mediator)
    {
        Register<ModulePackageOperationsChanged>();
        Register<ModuleOptionsChanged>();
    }

    public Task<ModuleMetadataResult> GetInstalledMetadata(CancellationToken cancellationToken = default)
        => GetMetadata(new GetModuleMetadataBundlesRequest(true, false, ResolveUpdates: true), cancellationToken);

    public Task<ModuleMetadataResult> GetAvailableMetadata(bool forceReload = false, CancellationToken cancellationToken = default)
        => GetMetadata(new GetModuleMetadataBundlesRequest(false, true, forceReload), cancellationToken);

    private async Task<ModuleMetadataResult> GetMetadata(GetModuleMetadataBundlesRequest request, CancellationToken cancellationToken)
    {
        var response = await Mediator.Request<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>(request, cancellationToken);

        if (response.RequestError is not null)
            throw new InvalidOperationException($"Failed to retrieve module metadata: {response.RequestError.Message}");

        // A slave sends the request to the master, which returns both halves while it runs a version without the filter.
        var bundles = response.Bundles
            .Where(k => k.Installed ? request.Installed : request.Available)
            .ToList();

        return new ModuleMetadataResult(ModuleMetadataModelFactory.CreateModels(bundles), response.AllowInstallation);
    }

    public async Task<IModuleManagementServiceResult> UpdateOptions(string moduleId, IEnumerable<ModuleOptionDeclaration> options, CancellationToken cancellationToken = default)
    {
        var command = new UpdateModuleOptions(moduleId, [.. options.Where(k => k.Value != Core.Shared.Constants.SetByEnvironmentMarker)]);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IModuleManagementServiceResult> UpdateOperations(List<ModulePackageOperation> operations, CancellationToken cancellationToken = default)
    {
        if (operations.Count == 0)
            return CreateSuccessResult();

        var command = new UpdateModulePackageOperations(operations);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    protected override IModuleManagementServiceResult CreateSuccessResult()
        => new ModuleManagementServiceSuccessResult();

    protected override IModuleManagementServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new ModuleManagementServiceErrorResult(errorMessage, errorCode);

    public async Task Consume(ClientContext<ModulePackageOperationsChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.Error);
            return;
        }

        // The command completes as soon as the event arrives.
        CompleteWithSuccess(context.Message.CorrelationId);

        // Non-error changes reach the service's subscribers.
        await NotifyOperationsChanged(context.Message, cancellationToken);
    }


    public async Task Consume(ClientContext<ModuleOptionsChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.Error);
            return;
        }

        // The command completes as soon as the event arrives.
        CompleteWithSuccess(context.Message.CorrelationId);

        // Non-error changes reach the service's subscribers.
        await NotifyOptionsChanged(context.Message, cancellationToken);
    }

    private async Task NotifyOperationsChanged(ModulePackageOperationsChanged change, CancellationToken cancellationToken)
    {
        if (OperationsChanged is not null)
            await OperationsChanged.Invoke(change, cancellationToken);
    }

    private async Task NotifyOptionsChanged(ModuleOptionsChanged change, CancellationToken cancellationToken)
    {
        if (OptionsChanged is not null)
            await OptionsChanged.Invoke(change, cancellationToken);
    }
}
