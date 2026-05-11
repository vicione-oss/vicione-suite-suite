using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Events;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class CreateRoleConsumer(RoleManager<SuiteRole> roleManager, ILogger<CreateRoleConsumer> logger) : IConsumer<CreateRole>
{
    public async Task Consume(ConsumeContext<CreateRole> context)
    {
        var correlationId = context.Message.CorrelationId;
        var role = context.Message.Role;

        LogConsume(logger, correlationId, role.Name);

        try
        {
            var existingRole = await roleManager.FindByNameAsync(role.Name);
            if (existingRole is not null)
            {
                LogRoleAlreadyExists(logger, correlationId, role.Name);

                var errorInfo = new ErrorInfo(RoleErrorCodes.CreateFailedAlreadyExists,
                        $"Could not add role '{role.Name}'. A role with that name already exists.");
                var errorResponse = new RoleCreatedEvent(role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var result = await roleManager.CreateAsync(role.ToSuiteRole());
            if (!result.Succeeded)
            {
                LogFailedToCreateRole(logger, correlationId, role.Name);

                var errorInfo = new ErrorInfo(RoleErrorCodes.CreateFailed,
                        $"Could not add role '{role.Name}'. Error: {string.Join(Environment.NewLine, result.Errors.Select(e => e.Description))}");
                var errorResponse = new RoleCreatedEvent(role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var addedRole = roleManager.Roles.First(r => r.Name == role.Name);

            LogRoleCreated(logger, correlationId, role.Name);

            foreach (var claim in role.Claims)
                await roleManager.AddClaimAsync(addedRole, claim.ToClaim());

            var response = new RoleCreatedEvent(role)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId, role.Name);

            var errorInfo = new ErrorInfo(RoleErrorCodes.CreateFailed, $"Could not add role '{role.Name}'. {ex.Message}.");
            var errorResponse = new RoleCreatedEvent(role)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating role '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<CreateRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created role '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogRoleCreated(ILogger<CreateRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create role '{Name}' correlated by {CorrelationId} because it already exists.")]
    private static partial void LogRoleAlreadyExists(ILogger<CreateRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create role '{Name}' by manager correlated by {CorrelationId}.")]
    private static partial void LogFailedToCreateRole(ILogger<CreateRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error on creating role '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogUnexpectedError(ILogger<CreateRoleConsumer> logger, Exception ex, Guid correlationId, string name);
}
