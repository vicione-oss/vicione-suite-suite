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

namespace Blazor.Shared.Profile.ControlPanels.Passkeys;

public class RenamePasskeyControlPanelSaveHandler :
    ControlPanelSaveHandlerBase<RenamePasskeyControlPanelState>,
    IEventConsumer<PasskeyRenamingCompleted>
{
    public RenamePasskeyControlPanelSaveHandler(IUiMediator mediator) : base(mediator)
    {
        Register<PasskeyRenamingCompleted>();
    }

    public override async Task<ISaveResult> Save(RenamePasskeyControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.UserId is null)
            return new SaveErrorResult(Localization.ValidationMessages.UserIsNotSet);

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
            PasskeyError.UserNotFound => Localization.ValidationMessages.PasskeyUserNotFound,
            PasskeyError.PasskeyNotFound => Localization.ValidationMessages.PasskeyNotFound,
            PasskeyError.NameInvalid => Localization.ValidationMessages.PasskeyNameInvalid,
            PasskeyError.NameAlreadyInUse => Localization.ValidationMessages.PasskeyNameAlreadyInUse,
            _ => Localization.ValidationMessages.PasskeyRenameFailed
        });

    private static bool NameIsAlreadyTaken(RenamePasskeyControlPanelState state)
        => state.ExistingNames
            .Where(name => !string.Equals(name, state.CurrentName, StringComparison.OrdinalIgnoreCase))
            .Contains(state.NewName, StringComparer.OrdinalIgnoreCase);
}
