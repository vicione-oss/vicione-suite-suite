using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Commands;
using Core.Shared.Passkeys.Events;
using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Passkeys;

public sealed partial class RenamePasskeyConsumer(
    UserManager<SuiteUser> userManager,
    ILogger<RenamePasskeyConsumer> logger) :
    IConsumer<RenamePasskey>
{
    public async Task Consume(ConsumeContext<RenamePasskey> context)
    {
        try
        {
            var user = await userManager.FindByIdAsync(context.Message.UserId);
            if (user is null)
            {
                await PublishError(context, PasskeyError.UserNotFound);
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Message.NewName) || context.Message.NewName.Length > Constants.MaxPasskeyNameLength)
            {
                await PublishError(context, PasskeyError.NameInvalid);
                return;
            }

            var credentialId = PasskeyIdConverter.DecodePasskeyId(context.Message.PasskeyId);
            var passkeys = await userManager.GetPasskeysAsync(user);
            var passkey = passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialId));
            if (passkey is null)
            {
                await PublishError(context, PasskeyError.PasskeyNotFound);
                return;
            }

            if (IsNameTakenByAnotherPasskey(passkeys, passkey, context.Message.NewName))
            {
                await PublishError(context, PasskeyError.NameAlreadyInUse);
                return;
            }

            if (IsUpdatedName(context, passkey))
            {
                var result = await UpdatePasskey(context, passkey, user);
                if (!result.Succeeded)
                {
                    await HandleFailedUpdate(context, result);
                    return;
                }
            }

            await context.Publish(new PasskeyRenamingCompleted(context.Message.CorrelationId));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error renaming passkey");
            await PublishError(context, PasskeyError.RenameFailed);
        }
    }

    private static bool IsUpdatedName(ConsumeContext<RenamePasskey> context, UserPasskeyInfo passkey)
        => !string.Equals(passkey.Name, context.Message.NewName, StringComparison.Ordinal);

    private static bool IsNameTakenByAnotherPasskey(IList<UserPasskeyInfo> passkeys,
        UserPasskeyInfo current,
        string newName)
        => passkeys
            .Where(p => !p.CredentialId.SequenceEqual(current.CredentialId))
            .Any(p => string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase));

    private async Task<IdentityResult> UpdatePasskey(ConsumeContext<RenamePasskey> context,
        UserPasskeyInfo passkey,
        SuiteUser user)
    {
        LogRenamingPasskeyWithId(passkey.CredentialId.Take(8).ToArray(), passkey.Name, context.Message.NewName);
        passkey.Name = context.Message.NewName;
        return await userManager.AddOrUpdatePasskeyAsync(user, passkey);
    }

    private async Task HandleFailedUpdate(ConsumeContext<RenamePasskey> context, IdentityResult result)
    {
        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        LogFailedToRenamePasskeyErrors(errors);
        await PublishError(context, PasskeyError.RenameFailed);
    }

    private static Task PublishError(ConsumeContext<RenamePasskey> context, PasskeyError error)
        => context.Publish(new PasskeyRenamingCompleted(context.Message.CorrelationId)
        {
            ErrorInfo = new ErrorInfo((int)error, null)
        });

    [LoggerMessage(LogLevel.Debug, "Renaming passkey with id {Id} from {OldName} to {NewName}")]
    partial void LogRenamingPasskeyWithId(byte[] Id, string? OldName, string NewName);

    [LoggerMessage(LogLevel.Error, "Failed to rename passkey: ({Errors})")]
    partial void LogFailedToRenamePasskeyErrors(string Errors);
}
