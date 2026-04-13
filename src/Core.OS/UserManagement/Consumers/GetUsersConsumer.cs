using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed class GetUsersConsumer(UserManager<SuiteUser> userManager, ILogger<GetUsersConsumer> logger)
    : RequestConsumer<GetUsers, GetUsersResponse>
{
    public override async Task<GetUsersResponse> Respond(GetUsers message, CancellationToken cancellationToken)
    {
        if (message.UserName?.Value is not null)
        {
            var suiteUser = await userManager.FindByNameAsync(message.UserName.Value.Value);
            if (suiteUser is null)
                return new GetUsersResponse([], new ErrorInfo(UserErrorCodes.NotFound, $"Could not find user '{message.UserName}'"));
            var user = await suiteUser.CreateUserProfile(userManager);
            return new GetUsersResponse([user]);
        }

        //Get
        var suiteUsers = await userManager.Users.ToListAsync(cancellationToken);
        var users = await Task.WhenAll(suiteUsers.Select(async u => await u.CreateUserProfile(userManager)));

        return new GetUsersResponse([.. users]);
    }

    public override Task<GetUsersResponse> HandleException(GetUsers message, Exception e, CancellationToken cancellationToken)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetUsers));

        return Task.FromResult(new GetUsersResponse([], new ErrorInfo(UserErrorCodes.UnknownError, e.Message)));
    }
}
