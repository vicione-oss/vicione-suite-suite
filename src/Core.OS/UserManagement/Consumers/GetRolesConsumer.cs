using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Requests;

namespace Core.OS.UserManagement.Consumers;

public sealed class GetRolesConsumer(RoleManager<SuiteRole> roleManager, ILogger<GetRolesConsumer> logger) :
    RequestConsumer<GetRoles, GetRolesResponse>
{
    protected override async Task<GetRolesResponse> Respond(ConsumeContext<GetRoles> context)
    {
        logger.LogInformation("Consuming {Request} with CorrelationId '{Id}'", nameof(GetRoles), context.CorrelationId);
        var availableRoles = new List<Role>();

        //Get
        var roles = await roleManager.Roles.ToListAsync(context.CancellationToken);

        foreach (var role in roles)
        {
            var claims = new List<UserManagementClaim>((await roleManager.GetClaimsAsync(role)).Select(c => c.ToUserManagementClaim()));

            availableRoles.Add(role.ToRole(claims));
        }

        return new GetRolesResponse(availableRoles);
    }

    protected override Task<GetRolesResponse> HandleException(ConsumeContext<GetRoles> context, Exception e)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetRoles));

        return Task.FromResult(new GetRolesResponse([], new(UserErrorEvent.UnknownError, e.Message)));
    }
}
