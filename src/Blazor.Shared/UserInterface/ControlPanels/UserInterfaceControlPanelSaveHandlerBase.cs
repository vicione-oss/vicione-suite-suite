using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserInterface.ControlPanels;

internal abstract class UserInterfaceControlPanelSaveHandlerBase<TState> : ControlPanelSaveHandlerBase<TState>,
    IEventConsumer<CrossInstanceConfigurationChanged>,
    IEventConsumer<CrossInstanceConfigurationError>
    where TState : ControlPanelState

{
    public UserInterfaceControlPanelSaveHandlerBase(IUiMediator mediator) : base(mediator)
    {
        Register<CrossInstanceConfigurationChanged>();
        Register<CrossInstanceConfigurationError>();
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationChanged> context, CancellationToken cancellationToken)
    {
        CompleteWithSuccess(context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationError> context, CancellationToken cancellationToken = default)
    {
        CompleteWithError(context.Message.CorrelationId, context.Message.Error);

        return Task.CompletedTask;
    }

    protected abstract Task<UserInterfaceSaveResult> SaveInternal(TState state, CancellationToken cancellationToken);

    protected virtual Task AfterSaveInternal(TState state, ISaveResult? saveResult, CancellationToken cancellationToken) => Task.CompletedTask;

    public override async Task<ISaveResult> Save(TState state, CancellationToken cancellationToken)
    {
        var result = await SaveInternal(state, cancellationToken);

        if (result.ErrorSaveResult is not null)
        {
            return result.ErrorSaveResult;
        }

        var command = new SetCrossInstanceConfiguration(result.CultureName, result.TimeZoneId);
        var saveResult = await SendAndWaitForCompletion(command, cancellationToken);

        await AfterSaveInternal(state, saveResult, cancellationToken);

        return saveResult;
    }
}
