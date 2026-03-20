using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Requests;

namespace Core.OS.UserManagement.Consumers;

public sealed class GetRolesConsumer(RoleManager<SuiteRole> roleManager, ILogger<GetRolesConsumer> logger) :
    RequestConsumer<GetRoles, GetRolesResponse>
{
    public override async Task<GetRolesResponse> Respond(GetRoles message, CancellationToken cancellationToken)
    {
        var availableRoles = new List<Role>();

        //Get
        var roles = await roleManager.Roles.ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            var claims = new List<UserManagementClaim>((await roleManager.GetClaimsAsync(role)).Select(c => c.ToUserManagementClaim()));

            availableRoles.Add(role.ToRole(claims));
        }

        return new GetRolesResponse(availableRoles);
    }

    public override Task<GetRolesResponse> HandleException(GetRoles message, Exception e, CancellationToken cancellationToken)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetRoles));

        return Task.FromResult(new GetRolesResponse([], new(UserErrorEvent.UnknownError, e.Message)));
    }
}
