using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Events;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class DeleteRoleConsumer(RoleManager<SuiteRole> roleManager, ILogger<DeleteRoleConsumer> logger) : IConsumer<DeleteRole>
{
    public async Task Consume(ConsumeContext<DeleteRole> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
            var role = await roleManager.FindByNameAsync(context.Message.Role.Name);
            if (role is null)
            {
                var errorInfo = new ErrorInfo(RoleErrorCodes.DeleteFailedNotFound,
                        $"Could not delete role '{context.Message.Role.Name}'. No role was found with that name");
                var errorResponse = new RoleDeletedEvent(context.Message.Role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            if (role.Managed is true)
            {
                var errorInfo = new ErrorInfo(RoleErrorCodes.DeleteFailed,
                        $"Delete role '{context.Message.Role.Name}' cancelled. Default roles can't be deleted");
                var errorResponse = new RoleDeletedEvent(context.Message.Role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            await roleManager.DeleteAsync(role);

            var response = new RoleDeletedEvent(context.Message.Role)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, context.Message.Role.Name, correlationId);

            var errorInfo = new ErrorInfo(RoleErrorCodes.CreateFailed,
                        $"Could not delete role '{context.Message.Role.Name}'. {ex.Message}.");
            var errorResponse = new RoleDeletedEvent(context.Message.Role)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete role '{name}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<DeleteRoleConsumer> logger, Exception ex, string name, Guid correlationId);
}
