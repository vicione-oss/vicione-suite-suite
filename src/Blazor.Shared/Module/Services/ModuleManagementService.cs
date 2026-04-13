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

    public async Task<List<ModuleMetadataModel>> GetMetadata(bool forceReload = false, CancellationToken cancellationToken = default)
    {
        var request = new GetModuleMetadataBundlesRequest(true, true, forceReload);
        var response = await Mediator.Request<GetModuleMetadataBundlesRequest, GetModuleMetadataBundlesResponse>(request, cancellationToken);

        if (response.RequestError is not null)
            throw new InvalidOperationException($"Failed to retrieve module metadata: {response.RequestError.Message}");

        return ModuleMetadataModelFactory.CreateModels(response.Bundles);
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

        // we complete the command as soon as we receive the event
        CompleteWithSuccess(context.Message.CorrelationId);

        // we propagate non error changes to the subscribers of the service
        await NotifyOperationsChanged(context.Message, cancellationToken);
    }


    public async Task Consume(ClientContext<ModuleOptionsChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.Error);
            return;
        }

        // we complete the command as soon as we receive the event
        CompleteWithSuccess(context.Message.CorrelationId);

        // we propagate non error changes to the subscribers of the service
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
