using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed class DeleteUserConsumer(UserManager<SuiteUser> userManager, ILogger<DeleteUserConsumer> logger) : IConsumer<DeleteUser>
{
    public async Task Consume(ConsumeContext<DeleteUser> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogInformation("Consuming {Command} with CorrelationId '{Id}'", nameof(DeleteUser), correlationId);

        var user = await userManager.FindByNameAsync(context.Message.UserProfile.UserName.Value);
        if (user is null)
        {
            await context.Publish(new UserErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.DeleteFailedNotFound,
                    $"Could not delete user '{context.Message.UserProfile.UserName}'. No user was found with that name"),
                context.Message.UserProfile.UserName));
            return;
        }

        if (await userManager.IsLastSystemAdministrator(user))
        {
            await context.Publish(new UserErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.SystemAdminLockout,
                    $"Delete user '{context.Message.UserProfile.UserName}' cancelled. Last system administrator can't be deleted"),
                context.Message.UserProfile.UserName));
            return;
        }

        await userManager.DeleteAsync(user);
        await context.Publish(new UserDeletedEvent(correlationId, context.Message.UserProfile));
    }
}
