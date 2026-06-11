using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class DeleteUserConsumer(UserManager<SuiteUser> userManager, ILogger<DeleteUserConsumer> logger) : IConsumer<DeleteUser>
{
    public async Task Consume(ConsumeContext<DeleteUser> context)
    {
        var correlationId = context.Message.CorrelationId;
        var userProfile = context.Message.UserProfile;

        LogConsume(logger, correlationId, userProfile.UserName.Value);

        try
        {
            var user = await userManager.FindByNameAsync(userProfile.UserName.Value);
            if (user is null)
            {
                // ADR-002: redelivery after successful delete must still complete the orchestration.
                LogUserAlreadyDeleted(logger, correlationId, userProfile.UserName.Value);

                var idempotentResponse = new UserDeletedEvent(userProfile)
                {
                    CorrelationId = correlationId
                };

                await context.Publish(idempotentResponse, context.CancellationToken);
                return;
            }

            if (await userManager.IsLastSystemAdministrator(user))
            {
                LogLastAdministrator(logger, correlationId, userProfile.UserName.Value);

                var errorInfo = new ErrorInfo(UserErrorCodes.SystemAdminLockout,
                             $"Delete user '{context.Message.UserProfile.UserName}' cancelled. Last system administrator can't be deleted");
                var errorResponse = new UserDeletedEvent(context.Message.UserProfile)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            await userManager.DeleteAsync(user);

            LogUserDeleted(logger, correlationId, userProfile.UserName.Value);

            var response = new UserDeletedEvent(context.Message.UserProfile)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId, userProfile.UserName.Value);

            var errorInfo = new ErrorInfo(UserErrorCodes.DeleteFailed,
                         $"Could not delete user '{userProfile.UserName}'. {ex.Message}.");
            var errorResponse = new UserDeletedEvent(userProfile)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting user '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<DeleteUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted user '{Name}' correlated by '{CorrelationId}'.")]
    private static partial void LogUserDeleted(ILogger<DeleteUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "User '{Name}' correlated by '{CorrelationId}' was already deleted; publishing completion event idempotently.")]
    private static partial void LogUserAlreadyDeleted(ILogger<DeleteUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete user '{Name}' correlated by '{CorrelationId}' because it is last administrator.")]
    private static partial void LogLastAdministrator(ILogger<DeleteUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error occured deleting user '{Name}' correlated by '{CorrelationId}'.")]
    private static partial void LogUnexpectedError(ILogger<DeleteUserConsumer> logger, Exception ex, Guid correlationId, string name);
}
