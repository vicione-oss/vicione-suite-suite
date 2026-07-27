using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Commands;
using Core.Shared.Passkeys.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using ViciOne.Ui.Localization.Resources;
using PasskeyConstants = Core.Shared.Passkeys.Constants;
using RenamePasskeyCommand = Core.Shared.Passkeys.Commands.RenamePasskey;

namespace Blazor.Shared.Profile.ControlPanels.Passkeys.Services;

public class EditPasskeyControlPanelSaveHandler :
    ControlPanelSaveHandlerBase<EditPasskeyControlPanelState>,
    IEventConsumer<PasskeyRenamingCompleted>
{
    public EditPasskeyControlPanelSaveHandler(IUiMediator mediator) : base(mediator)
    {
        Register<PasskeyRenamingCompleted>();
    }

    public override async Task<ISaveResult> Save(EditPasskeyControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.UserId is null)
            return new SaveErrorResult(Localization.EditPasskeyControlPanelSaveHandler.UserIsNotSet);

        if (string.IsNullOrWhiteSpace(state.NewName))
            return new SaveErrorResult(string.Format(ValidationMessages.Culture,
                ValidationMessages.FieldIsRequired,
                nameof(state.NewName)));

        if (state.NewName.Length > PasskeyConstants.MaxPasskeyNameLength)
            return new SaveErrorResult(string.Format(ValidationMessages.Culture,
                ValidationMessages.FieldMustNotHaveMoreThanXCharacters,
                nameof(state.NewName),
                PasskeyConstants.MaxPasskeyNameLength));

        if (NameIsAlreadyTaken(state))
            return new SaveErrorResult(
                string.Format(ValidationMessages.Culture, ValidationMessages.FieldHasToBeUnique, nameof(state.NewName)));

        var request = new RenamePasskeyCommand
        {
            UserId = state.UserId,
            PasskeyId = state.PasskeyId!,
            NewName = state.NewName
        };

        return await SendAndWaitForCompletion(request, cancellationToken);
    }

    public Task Consume(ClientContext<PasskeyRenamingCompleted> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is null)
            CompleteWithSuccess(context.Message.CorrelationId);
        else
            CompleteWithError(context.Message.CorrelationId, Localize(context.Message.ErrorInfo));

        return Task.CompletedTask;
    }

    private static ErrorInfo Localize(ErrorInfo errorInfo)
        => new(errorInfo.ErrorCode, (PasskeyError)errorInfo.ErrorCode switch
        {
            PasskeyError.UserNotFound => Localization.EditPasskeyControlPanelSaveHandler.PasskeyUserNotFound,
            PasskeyError.PasskeyNotFound => Localization.EditPasskeyControlPanelSaveHandler.PasskeyNotFound,
            PasskeyError.NameInvalid => Localization.EditPasskeyControlPanelSaveHandler.PasskeyNameInvalid,
            PasskeyError.NameAlreadyInUse => Localization.EditPasskeyControlPanelSaveHandler.PasskeyNameAlreadyInUse,
            _ => Localization.EditPasskeyControlPanelSaveHandler.PasskeyRenameFailed
        });

    private static bool NameIsAlreadyTaken(EditPasskeyControlPanelState state)
        => state.ExistingNames
            .Where(name => !string.Equals(name, state.CurrentName, StringComparison.OrdinalIgnoreCase))
            .Contains(state.NewName, StringComparer.OrdinalIgnoreCase);
}
