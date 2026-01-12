using System.Security.Claims;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Sdk.Messaging;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Events;


namespace Core.OS.UserManagement.Consumers;

public sealed class UpdateRoleConsumer(RoleManager<SuiteRole> roleManager,
    IEqualityComparer<Claim> claimEqualityComparer, ILogger<UpdateRoleConsumer> logger)
        : IConsumer<UpdateRole>
{
    public async Task Consume(ConsumeContext<UpdateRole> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogInformation("Consuming {Command} with CorrelationId '{Id}'", nameof(UpdateRole), correlationId);

        var suiteRole = await roleManager.FindByNameAsync(context.Message.Role.Name);
        if (suiteRole is null)
        {
            await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.UpdateFailedNotFound,
                    $"Could not update role '{context.Message.Role.Name}'. No role was found with that name"),
                context.Message.Role));
            return;
        }

        if (suiteRole.Managed is true)
        {
            await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.UpdateFailed,
                    $"Update role '{context.Message.Role.Name}' cancelled. Default roles can't be edited."),
                context.Message.Role));
            return;
        }

        if (suiteRole.Description != context.Message.Role.Description)
            suiteRole.Description = context.Message.Role.Description;

        try
        {
            await roleManager.UpdateAsync(suiteRole);
            await AssignClaims(context.Message.Role.Claims, suiteRole);

            await context.Publish(new RoleUpdatedEvent(correlationId, suiteRole.ToRole(context.Message.Role.Claims)));
        }
        catch (Exception ex)
        {
            await context.Publish(new RoleErrorEvent(correlationId, new ErrorInfo(UserErrorEvent.UpdateFailed,
                    $"Could not update role '{context.Message.Role.Name}'. {ex.Message}."),
                context.Message.Role));
            return;
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
}
