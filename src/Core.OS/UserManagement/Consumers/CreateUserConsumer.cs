using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class CreateUserConsumer(UserManager<SuiteUser> userManager, ILogger<CreateUserConsumer> logger) : IConsumer<CreateUser>
{
    public async Task Consume(ConsumeContext<CreateUser> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
            var user = await userManager.FindByNameAsync(context.Message.UserProfile.UserName.Value);
            if (user is not null)
            {
                var errorInfo = new ErrorInfo(UserErrorCodes.CreateFailedAlreadyExists,
                             $"Could not add user '{context.Message.UserProfile.UserName}'. A user with that name already exists.");
                var errorResponse = new UserCreatedEvent(context.Message.UserProfile)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }
            if (context.Message.UserProfile.NewPassword is null)
            {
                var errorInfo = new ErrorInfo(UserErrorCodes.CreateFailedMissingPw,
                             $"Could not add user '{context.Message.UserProfile.UserName}'. No password was provided.");
                var errorResponse = new UserCreatedEvent(context.Message.UserProfile)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var suiteUser = new SuiteUser
            {
                UserName = context.Message.UserProfile.UserName.Value,
                Email = context.Message.UserProfile.Email,
                EmailConfirmed = false
            };
            suiteUser.AssignOptionalData(context.Message.UserProfile);

            var result = await userManager.CreateAsync(suiteUser, context.Message.UserProfile.NewPassword);
            if (!result.Succeeded)
            {
                var errorInfo = new ErrorInfo(UserErrorCodes.CreateFailed,
                         $"Could not add user '{context.Message.UserProfile.UserName}'. Error: {string.Join(Environment.NewLine, result.Errors.Select(e => e.Description))}");
                var errorResponse = new UserCreatedEvent(context.Message.UserProfile)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var claims = context.Message.UserProfile.Claims.Select(userProfileClaim => userProfileClaim.ToClaim());
            await userManager.AddToRolesAsync(suiteUser, context.Message.UserProfile.Roles);
            await userManager.AddClaimsAsync(suiteUser, claims);

            var response = new UserCreatedEvent(context.Message.UserProfile)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, context.Message.UserProfile.UserName.Value, correlationId);

            var errorInfo = new ErrorInfo(UserErrorCodes.CreateFailed,
                         $"Could not add user '{context.Message.UserProfile.UserName}'. {ex.Message}.");
            var errorResponse = new UserCreatedEvent(context.Message.UserProfile)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create user '{name}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<CreateUserConsumer> logger, Exception ex, string name, Guid correlationId);
}
