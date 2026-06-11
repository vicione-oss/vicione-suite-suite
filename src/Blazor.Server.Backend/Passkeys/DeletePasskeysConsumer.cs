using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Commands;
using Core.Shared.Passkeys.Events;
using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Passkeys;

public sealed partial class DeletePasskeysConsumer(
    UserManager<SuiteUser> userManager,
    ILogger<DeletePasskeysConsumer> logger) :
    IConsumer<DeletePasskeys>
{
    public async Task Consume(ConsumeContext<DeletePasskeys> context)
    {
        try
        {
            var user = await userManager.FindByIdAsync(context.Message.UserId);
            if (user is null)
            {
                await PublishPasskeyError(context, 404, "User not found");
                return;
            }

            await DeletePasskeys(context, user);
            await PublishPasskeyDeletionCompletedSuccessfully(context);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during deletion of passkeys");
            await PublishPasskeyError(context, 400, "Deletion failed");
        }
    }

    private async Task DeletePasskeys(ConsumeContext<DeletePasskeys> context, SuiteUser user)
    {
        foreach (var stringId in context.Message.PasskeyIds)
        {
            var credentialId = PasskeyIdConverter.DecodePasskeyId(stringId);
            LogDeletingPasskeyWithId(credentialId);
            await userManager.RemovePasskeyAsync(user, credentialId);
        }
    }

    private static async Task PublishPasskeyError(ConsumeContext<DeletePasskeys> context,
        int errorCode,
        string? message)
        => await context.Publish(
            new PasskeyDeletionCompleted(context.Message.CorrelationId)
            {
                ErrorInfo = new ErrorInfo(errorCode, message)
            });

    private static async Task PublishPasskeyDeletionCompletedSuccessfully(ConsumeContext<DeletePasskeys> context)
        => await context.Publish(new PasskeyDeletionCompleted(context.Message.CorrelationId));

    [LoggerMessage(LogLevel.Debug, "Deleting passkey with id {Id}")]
    partial void LogDeletingPasskeyWithId(byte[] id);
}
