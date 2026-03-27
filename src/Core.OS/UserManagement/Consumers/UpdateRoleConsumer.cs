using System.Security.Claims;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Events;


namespace Core.OS.UserManagement.Consumers;

public sealed partial class UpdateRoleConsumer(RoleManager<SuiteRole> roleManager,
    IEqualityComparer<Claim> claimEqualityComparer, ILogger<UpdateRoleConsumer> logger)
        : IConsumer<UpdateRole>
{
    public async Task Consume(ConsumeContext<UpdateRole> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
            var suiteRole = await roleManager.FindByNameAsync(context.Message.Role.Name);
            if (suiteRole is null)
            {
                var errorInfo = new ErrorInfo(RoleErrorCodes.UpdateFailedNotFound,
                            $"Could not update role '{context.Message.Role.Name}'. No role was found with that name");
                var errorResponse = new RoleUpdatedEvent(context.Message.Role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            if (suiteRole.Managed is true)
            {
                var errorInfo = new ErrorInfo(RoleErrorCodes.UpdateFailed,
                            $"Update role '{context.Message.Role.Name}' cancelled. Default roles can't be edited.");
                var errorResponse = new RoleUpdatedEvent(context.Message.Role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            if (suiteRole.Description != context.Message.Role.Description)
                suiteRole.Description = context.Message.Role.Description;

            await roleManager.UpdateAsync(suiteRole);
            await AssignClaims(context.Message.Role.Claims, suiteRole);

            var response = new RoleUpdatedEvent(suiteRole.ToRole(context.Message.Role.Claims)) { CorrelationId = correlationId };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, context.Message.Role.Name, correlationId);

            var errorInfo = new ErrorInfo(RoleErrorCodes.UpdateFailed,
                        $"Could not update role '{context.Message.Role.Name}'. {ex.Message}.");
            var errorResponse = new RoleUpdatedEvent(context.Message.Role)
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };

            await context.Publish(errorResponse, context.CancellationToken);
        }
    }


    private async Task AssignClaims(IEnumerable<UserManagementClaim> claims, SuiteRole role)
    {
        var existingClaims = await roleManager.GetClaimsAsync(role);
        var intendedClaims = claims.Select(userProfileClaim => userProfileClaim.ToClaim()).ToArray();

        // remove obsolete claims
        {
            foreach (var existingClaim in existingClaims)
            {
                if (intendedClaims.Contains(existingClaim, claimEqualityComparer))
                    continue;

                await roleManager.RemoveClaimAsync(role, existingClaim);
            }
        }

        // add new claims
        {
            foreach (var intendedClaim in intendedClaims)
            {
                if (existingClaims.Contains(intendedClaim, claimEqualityComparer))
                    continue;

                await roleManager.AddClaimAsync(role, intendedClaim);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update role '{name}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<UpdateRoleConsumer> logger, Exception ex, string name, Guid correlationId);
}
