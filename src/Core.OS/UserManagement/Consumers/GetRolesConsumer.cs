using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Requests;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class GetRolesConsumer(RoleManager<SuiteRole> roleManager, ILogger<GetRolesConsumer> logger) :
    RequestConsumer<GetRoles, GetRolesResponse>
{
    public override async Task<GetRolesResponse> Respond(GetRoles message, CancellationToken cancellationToken)
    {
        var availableRoles = new List<Role>();

        //Get
        var roles = await roleManager.Roles.ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            var roleClaims = await roleManager.GetClaimsAsync(role);
            var claims = new List<UserManagementClaim>(roleClaims.Select(c => c.ToUserManagementClaim()));

            availableRoles.Add(role.ToRole(claims));
        }

        return new GetRolesResponse(availableRoles);
    }

    public override Task<GetRolesResponse> HandleException(GetRoles message, Exception e, CancellationToken cancellationToken)
    {
        LogRequestError(logger, e);

        return Task.FromResult(new GetRolesResponse([], new(UserErrorCodes.UnknownError, e.Message)));
    }

    [LoggerMessage(LogLevel.Error, "Failed to get roles")]
    private static partial void LogRequestError(ILogger<GetRolesConsumer> logger, Exception exception);
}
