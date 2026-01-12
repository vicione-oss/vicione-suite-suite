using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Requests;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed class GetRolesConsumer(RoleManager<IdentityRole> roleManager, ILogger<GetRolesConsumer> logger) :
    RequestConsumer<GetRoles, GetRolesResponse>
{
    protected override async Task<GetRolesResponse> Respond(ConsumeContext<GetRoles> context)
    {
        logger.LogInformation("Consuming {Request} with CorrelationId '{Id}'", nameof(GetRoles), context.CorrelationId);

        //Get
        var roles = await roleManager.Roles.Where(r => r.Name != null).Select(r => r.Name!).ToListAsync(context.CancellationToken);
        return new GetRolesResponse(roles);
    }

    protected override Task<GetRolesResponse> HandleException(ConsumeContext<GetRoles> context, Exception e)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetRoles));

        return Task.FromResult(new GetRolesResponse([], new(UserErrorEvent.UnknownError, e.Message)));
    }
}