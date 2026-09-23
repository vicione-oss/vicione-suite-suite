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
        var role = context.Message.Role;

        LogConsume(logger, correlationId, role.Name);

        try
        {
            var existingRole = await roleManager.FindByNameAsync(role.Name);
            if (existingRole is null)
            {
                LogRoleNotFound(logger, correlationId, role.Name);

                var errorInfo = new ErrorInfo(RoleErrorCodes.UpdateFailedNotFound,
                            $"Could not update role '{role.Name}'. No role was found with that name");
                var errorResponse = new RoleUpdatedEvent(role)
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

                var errorInfo = new ErrorInfo(RoleErrorCodes.UpdateFailed,
                            $"Update role '{role.Name}' cancelled. Default roles can't be edited.");
                var errorResponse = new RoleUpdatedEvent(role)
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(errorResponse, context.CancellationToken);
                return;
            }

            if (existingRole.Description != role.Description)
                existingRole.Description = role.Description;

            await roleManager.UpdateAsync(existingRole);
            await AssignClaims(role.Claims, existingRole);

            LogRoleUpdated(logger, correlationId, role.Name);

            var response = new RoleUpdatedEvent(existingRole.ToRole(role.Claims)) { CorrelationId = correlationId };

            await context.Publish(response, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId, role.Name);

            var errorInfo = new ErrorInfo(RoleErrorCodes.UpdateFailed,
                        $"Could not update role '{role.Name}'. {ex.Message}.");
            var errorResponse = new RoleUpdatedEvent(role)
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

        // Obsolete claims are removed.
        {
            foreach (var existingClaim in existingClaims)
            {
                if (intendedClaims.Contains(existingClaim, claimEqualityComparer))
                    continue;

                await roleManager.RemoveClaimAsync(role, existingClaim);
            }
        }

        // New claims are added.
        {
            foreach (var intendedClaim in intendedClaims)
            {
                if (existingClaims.Contains(intendedClaim, claimEqualityComparer))
                    continue;

                await roleManager.AddClaimAsync(role, intendedClaim);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Updating role '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<UpdateRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated role '{Name}' correlated by {CorrelationId}.")]
    private static partial void LogRoleUpdated(ILogger<UpdateRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update role '{Name}' correlated by {CorrelationId} because it was not found.")]
    private static partial void LogRoleNotFound(ILogger<UpdateRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update role '{Name}' correlated by {CorrelationId} because it is marked as managed.")]
    private static partial void LogRoleIsManaged(ILogger<UpdateRoleConsumer> logger, Guid correlationId, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error occured on updating role '{Name}' correlated by '{CorrelationId}'.")]
    private static partial void LogUnexpectedError(ILogger<UpdateRoleConsumer> logger, Exception ex, Guid correlationId, string name);
}
