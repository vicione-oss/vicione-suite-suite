using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Events;

namespace Core.OS.UserManagement.Consumers;

public sealed class DeleteRoleConsumer(RoleManager<SuiteRole> roleManager, ILogger<DeleteRoleConsumer> logger) : IConsumer<DeleteRole>
{
    public async Task Consume(ConsumeContext<DeleteRole> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogInformation("Consuming {Command} with CorrelationId '{Id}'", nameof(DeleteRole), correlationId);

        var role = await roleManager.FindByNameAsync(context.Message.Role.Name);
        if (role is null)
        {
            await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.DeleteFailedNotFound,
                    $"Could not delete role '{context.Message.Role.Name}'. No role was found with that name"),
                context.Message.Role));
            return;
        }

        if (role.Managed is true)
        {
            await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.DeleteFailed,
                    $"Delete role '{context.Message.Role.Name}' cancelled. Default roles can't be deleted"),
                context.Message.Role));
            return;
        }

        try
        {
            await roleManager.DeleteAsync(role);
        }
        catch (Exception ex)
        {
            await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.DeleteFailed,
                    $"Could not delete role '{context.Message.Role.Name}'. {ex.Message}."),
                context.Message.Role));
            return;
        }

        await context.Publish(new RoleDeletedEvent(correlationId, context.Message.Role));
    }
}
