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

        try
        {
            var user = await userManager.FindByNameAsync(context.Message.UserProfile.UserName.Value);
            if (user is null)
            {
                var errorInfo = new ErrorInfo(UserErrorCodes.DeleteFailedNotFound,
                             $"Could not delete user '{context.Message.UserProfile.UserName}'. No user was found with that name");
                var errorResponse = new UserDeletedEvent(context.Message.UserProfile)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            if (await userManager.IsLastSystemAdministrator(user))
            {
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

            var response = new UserDeletedEvent(context.Message.UserProfile)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, context.Message.UserProfile.UserName.Value, correlationId);

            var errorInfo = new ErrorInfo(UserErrorCodes.DeleteFailed,
                         $"Could not delete user '{context.Message.UserProfile.UserName}'. {ex.Message}.");
            var errorResponse = new UserDeletedEvent(context.Message.UserProfile)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete user '{name}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<DeleteUserConsumer> logger, Exception ex, string name, Guid correlationId);
}
