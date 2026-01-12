using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed class CreateUserConsumer(UserManager<SuiteUser> userManager, ILogger<CreateUserConsumer> logger) : IConsumer<CreateUser>
{
    public async Task Consume(ConsumeContext<CreateUser> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogInformation("Consuming {Command} with CorrelationId '{Id}'", nameof(CreateUser), correlationId);

        var user = await userManager.FindByNameAsync(context.Message.UserProfile.UserName.Value);
        if (user is not null)
        {
            await context.Publish(new UserErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.CreateFailedAlreadyExists,
                    $"Could not add user '{context.Message.UserProfile.UserName}'. A user with that name already exists."),
                context.Message.UserProfile.UserName));
            return;
        }
        if (context.Message.UserProfile.NewPassword is null)
        {
            await context.Publish(new UserErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.CreateFailedMissingPw,
                    $"Could not add user '{context.Message.UserProfile.UserName}'. No password was provided."),
                context.Message.UserProfile.UserName));
            return;
        }

        try
        {
            var suiteUser = new SuiteUser
            {
                UserName = context.Message.UserProfile.UserName.Value,
                Email = context.Message.UserProfile.Email,
                EmailConfirmed = false
            };
            suiteUser.AssignOptionalData(context.Message.UserProfile);

            var result = await userManager.CreateAsync(suiteUser, context.Message.UserProfile.NewPassword);
            if (result.Succeeded)
            {
                var claims = context.Message.UserProfile.Claims.Select(userProfileClaim => userProfileClaim.ToClaim());

                await userManager.AddToRolesAsync(suiteUser, context.Message.UserProfile.Roles);
                await userManager.AddClaimsAsync(suiteUser, claims);
                await context.Publish(new UserCreatedEvent(correlationId, context.Message.UserProfile));
                return;
            }
            await context.Publish(new UserErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.CreateFailed,
                    $"Could not add user '{context.Message.UserProfile.UserName}'. Error: {string.Join(Environment.NewLine, result.Errors.Select(e => e.Description))}"),
                context.Message.UserProfile.UserName));
        }
        catch (Exception ex)
        {
            await context.Publish(new UserErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.CreateFailed,
                    $"Could not add user '{context.Message.UserProfile.UserName}'. {ex.Message}."),
                context.Message.UserProfile.UserName));
        }
    }
}
