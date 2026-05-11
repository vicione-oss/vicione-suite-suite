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
        var userProfile = context.Message.UserProfile;

        LogConsume(logger, correlationId, userProfile.UserName.Value);

        try
        {
            if (await DoesUserAlreadyExist(context))
            {
                return;
            }

            if (await IsNewPasswordEmpty(context))
            {
                return;
            }

            var suiteUser = new SuiteUser
            {
                UserName = userProfile.UserName.Value,
                Email = userProfile.Email,
                EmailConfirmed = false
            };
            suiteUser.AssignOptionalData(userProfile);

            var result = await userManager.CreateAsync(suiteUser, userProfile.NewPassword!);
            if (!result.Succeeded)
            {
                LogCreateFailed(logger, correlationId, userProfile.UserName.Value);

                var errorInfo = new ErrorInfo(UserErrorCodes.CreateFailed,
                         $"Could not add user '{userProfile.UserName}'. Error: {string.Join(Environment.NewLine, result.Errors.Select(e => e.Description))}");
                var errorResponse = new UserCreatedEvent(userProfile)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            var claims = context.Message.UserProfile.Claims.Select(userProfileClaim => userProfileClaim.ToClaim());
            await userManager.AddToRolesAsync(suiteUser, userProfile.Roles);
            await userManager.AddClaimsAsync(suiteUser, claims);

            LogUserCreated(logger, correlationId, userProfile.UserName.Value);

            var response = new UserCreatedEvent(userProfile)
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId, userProfile.UserName.Value);

            var errorInfo = new ErrorInfo(UserErrorCodes.CreateFailed,
                         $"Could not add user '{userProfile.UserName}'. {ex.Message}.");
            var errorResponse = new UserCreatedEvent(userProfile)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }

    private async Task<bool> DoesUserAlreadyExist(ConsumeContext<CreateUser> context)
    {
        var correlationId = context.Message.CorrelationId;
        var userProfile = context.Message.UserProfile;

        var user = await userManager.FindByNameAsync(userProfile.UserName.Value);
        if (user is null)
            return false;

        LogAlreadyExists(logger, correlationId, userProfile.UserName.Value);

        var errorInfo = new ErrorInfo(UserErrorCodes.CreateFailedAlreadyExists,
                     $"Could not add user '{userProfile.UserName}'. A user with that name already exists.");
        var errorResponse = new UserCreatedEvent(userProfile)
        {
            CorrelationId = correlationId,
            ErrorInfo = errorInfo
        };

        await context.Publish(errorResponse, context.CancellationToken);
        return true;
    }

    private async Task<bool> IsNewPasswordEmpty(ConsumeContext<CreateUser> context)
    {
        var correlationId = context.Message.CorrelationId;
        var userProfile = context.Message.UserProfile;

        if (!string.IsNullOrEmpty(context.Message.UserProfile.NewPassword))
            return false;

        LogEmptyNewPassword(logger, correlationId, userProfile.UserName.Value);

        var errorInfo = new ErrorInfo(UserErrorCodes.CreateFailedMissingPw,
                     $"Could not add user '{userProfile.UserName}'. No password was provided.");
        var errorResponse = new UserCreatedEvent(userProfile)
        {
            CorrelationId = correlationId,
            ErrorInfo = errorInfo
        };

        await context.Publish(errorResponse, context.CancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating user '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<CreateUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created user '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogUserCreated(ILogger<CreateUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create user '{Name}' correlated by {CorrelationId} because it already exists.")]
    private static partial void LogAlreadyExists(ILogger<CreateUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create user '{Name}' correlated by {CorrelationId} because new password is empty.")]
    private static partial void LogEmptyNewPassword(ILogger<CreateUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create user '{Name}' by manager correlated by {CorrelationId}.")]
    private static partial void LogCreateFailed(ILogger<CreateUserConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error occured creating user '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogUnexpectedError(ILogger<CreateUserConsumer> logger, Exception ex, Guid correlationId, string name);
}
