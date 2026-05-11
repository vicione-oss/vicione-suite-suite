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
        var role = context.Message.Role;

        LogConsume(logger, correlationId, role.Name);

        try
        {
            var existingRole = await roleManager.FindByNameAsync(role.Name);
            if (existingRole is null)
            {
                LogRoleNotFound(logger, correlationId, role.Name);

                var errorInfo = new ErrorInfo(RoleErrorCodes.DeleteFailedNotFound,
                        $"Could not delete role '{role.Name}'. No role was found with that name");
                var errorResponse = new RoleDeletedEvent(role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            if (existingRole.Managed is true)
            {
                LogRoleIsManaged(logger, correlationId, role.Name);

                var errorInfo = new ErrorInfo(RoleErrorCodes.DeleteFailed,
                        $"Delete role '{role.Name}' cancelled. Default roles can't be deleted");
                var errorResponse = new RoleDeletedEvent(role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            await roleManager.DeleteAsync(existingRole);

            LogRoleDeleted(logger, correlationId, role.Name);

            var response = new RoleDeletedEvent(role)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId, role.Name);

            var errorInfo = new ErrorInfo(RoleErrorCodes.CreateFailed,
                        $"Could not delete role '{role.Name}'. {ex.Message}.");
            var errorResponse = new RoleDeletedEvent(role)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting role '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<DeleteRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted role '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogRoleDeleted(ILogger<DeleteRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete role '{Name}' correlated by {CorrelationId} because it was not found.")]
    private static partial void LogRoleNotFound(ILogger<DeleteRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete role '{Name}' correlated by {CorrelationId} because it is marked as managed.")]
    private static partial void LogRoleIsManaged(ILogger<DeleteRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error occured deleting role '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogUnexpectedError(ILogger<DeleteRoleConsumer> logger, Exception ex, Guid correlationId, string name);
}
