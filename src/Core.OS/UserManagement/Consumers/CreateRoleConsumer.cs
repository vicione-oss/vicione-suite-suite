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

        try
        {
            var role = await roleManager.FindByNameAsync(context.Message.Role.Name);
            if (role is not null)
            {
                var errorInfo = new ErrorInfo(RoleErrorCodes.CreateFailedAlreadyExists,
                        $"Could not add role '{context.Message.Role.Name}'. A role with that name already exists.");
                var errorResponse = new RoleCreatedEvent(context.Message.Role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var result = await roleManager.CreateAsync(context.Message.Role.ToSuiteRole());
            if (!result.Succeeded)
            {
                var errorInfo = new ErrorInfo(RoleErrorCodes.CreateFailed,
                        $"Could not add role '{context.Message.Role.Name}'. Error: {string.Join(Environment.NewLine, result.Errors.Select(e => e.Description))}");
                var errorResponse = new RoleCreatedEvent(context.Message.Role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var addedRole = roleManager.Roles.First(r => r.Name == context.Message.Role.Name);

            foreach (var claim in context.Message.Role.Claims)
                await roleManager.AddClaimAsync(addedRole, claim.ToClaim());

            var response = new RoleCreatedEvent(context.Message.Role)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, context.Message.Role.Name, correlationId);

            var errorInfo = new ErrorInfo(RoleErrorCodes.CreateFailed,
                        $"Could not add role '{context.Message.Role.Name}'. {ex.Message}.");
            var errorResponse = new RoleCreatedEvent(context.Message.Role)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create role '{name}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<CreateRoleConsumer> logger, Exception ex, string name, Guid correlationId);
}
