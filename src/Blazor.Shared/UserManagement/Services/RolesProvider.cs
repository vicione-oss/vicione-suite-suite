using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserManagement.Services;

public class RolesProvider(IUiMediator mediator, ILogger<RolesProvider> log) : IRolesProvider
{
    public async Task<IEnumerable<Role>> GetAvailableRoles(CancellationToken cancellationToken = default)
    {
        var rolesResponse = await mediator.Request<GetRoles, GetRolesResponse>(new GetRoles(), cancellationToken);
        if (rolesResponse.RequestError is not null)
        {
            log.LogError("Could not load available Roles - {ErrorMessage}", rolesResponse.RequestError.Message);
            return [];
        }

        return rolesResponse.Roles.Select(s => new Role(s));
    }
}
