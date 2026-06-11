using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace Blazor.Server.Backend.UserManagement;

public sealed partial class DeleteExternalLoginConsumer(
    UserManager<SuiteUser> userManager,
    ILogger<DeleteExternalLoginConsumer> logger) :
    IConsumer<DeleteExternalLogin>
{
    public async Task Consume(ConsumeContext<DeleteExternalLogin> context)
    {
        try
        {
            var user = await userManager.FindByIdAsync(context.Message.UserId);
            if (user is null)
            {
                await PublishError(context, ExternalLoginError.UserNotFound, "User not found");
                return;
            }

            if (await WouldRemoveLastCredential(user, context.Message.LoginProvider, context.Message.ProviderKey))
            {
                await PublishError(context, ExternalLoginError.LastCredential, "Removing this login would leave the user with no way to sign in");
                return;
            }

            var result = await userManager.RemoveLoginAsync(user, context.Message.LoginProvider, context.Message.ProviderKey);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                LogRemoveLoginFailedForUser(context.Message.UserId, errors);
                await PublishError(context, ExternalLoginError.Failed, "Deletion failed");
                return;
            }

            await context.Publish(new ExternalLoginDeletionCompleted(context.Message.CorrelationId));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error removing external login");
            await PublishError(context, ExternalLoginError.Failed, "Deletion failed");
        }
    }

    private async Task<bool> WouldRemoveLastCredential(SuiteUser user, string loginProvider, string providerKey)
    {
        if (await userManager.HasPasswordAsync(user))
            return false;

        var passkeys = await userManager.GetPasskeysAsync(user);
        if (passkeys.Count > 0)
            return false;

        var logins = await userManager.GetLoginsAsync(user);
        return !logins.Any(l => l.LoginProvider != loginProvider || l.ProviderKey != providerKey);
    }

    private static Task PublishError(ConsumeContext<DeleteExternalLogin> context, ExternalLoginError error, string? message)
        => context.Publish(new ExternalLoginDeletionCompleted(context.Message.CorrelationId)
        {
            ErrorInfo = new ErrorInfo((int)error, message)
        });

    [LoggerMessage(LogLevel.Error, "RemoveLoginAsync failed for user {UserId}: {Errors}")]
    partial void LogRemoveLoginFailedForUser(string userId, string errors);
}
