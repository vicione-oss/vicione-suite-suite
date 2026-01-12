using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Events;

namespace Core.OS.UserManagement.Consumers;

public class CreateRoleConsumer(RoleManager<SuiteRole> roleManager, ILogger<CreateRoleConsumer> logger) : IConsumer<CreateRole>
{
    public async Task Consume(ConsumeContext<CreateRole> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogInformation("Consuming {Command} with CorrelationId '{Id}'", nameof(CreateRole), correlationId);

        try
        {
            var role = await roleManager.FindByNameAsync(context.Message.Role.Name);
            if (role is not null)
            {
                await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(RoleErrorEvent.CreateFailedAlreadyExists,
                        $"Could not add role '{context.Message.Role.Name}'. A role with that name already exists."),
                    context.Message.Role));
                return;
            }

            var result = await roleManager.CreateAsync(context.Message.Role.ToSuiteRole());
            if (result.Succeeded)
            {
                var addedRole = roleManager.Roles.First(r => r.Name == context.Message.Role.Name);

                foreach (var claim in context.Message.Role.Claims)
                    await roleManager.AddClaimAsync(addedRole, claim.ToClaim());

                await context.Publish(new RoleCreatedEvent(correlationId, context.Message.Role));
                return;
            }
            await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(RoleErrorEvent.CreateFailed,
                    $"Could not add role '{context.Message.Role.Name}'. Error: {string.Join(Environment.NewLine, result.Errors.Select(e => e.Description))}"),
                context.Message.Role));
        }
        catch (Exception ex)
        {
            await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(RoleErrorEvent.CreateFailed,
                    $"Could not add role '{context.Message.Role.Name}'. {ex.Message}."),
                context.Message.Role));
        }
    }
}
