using Blazor.Shared.Profile.Localization;
using Core.Shared.Passkeys.Commands;
using Core.Shared.Passkeys.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class PasskeysControlPanelSaveHandler :
    ControlPanelSaveHandlerBase<PasskeysControlPanelState>,
    IEventConsumer<PasskeyDeletionCompleted>
{
    public PasskeysControlPanelSaveHandler(IUiMediator mediator) : base(mediator)
    {
        Register<PasskeyDeletionCompleted>();
    }

    public override async Task<ISaveResult> Save(PasskeysControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.User == null)
            return new SaveErrorResult(ValidationMessages.UserIsNotSet);

        var request = new DeletePasskeys
        {
            PasskeyIds = state.PasskeysMarkedForDeletion.ToArray(),
            UserId = state.User!.Id
        };
        var result = await SendAndWaitForCompletion(request, cancellationToken);
        if (result is SaveSuccessResult)
        {
            state.ClearMarkedPasskeys();
        }

        return result;
    }

    public Task Consume(ClientContext<PasskeyDeletionCompleted> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is null)
            CompleteWithSuccess(context.Message.CorrelationId);
        else
            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);

        return Task.CompletedTask;
    }
}
