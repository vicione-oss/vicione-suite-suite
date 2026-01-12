using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Requests;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed class GetUsersConsumer(UserManager<SuiteUser> userManager, ILogger<GetUsersConsumer> logger)
    : RequestConsumer<GetUsers, GetUsersResponse>
{
    protected override async Task<GetUsersResponse> Respond(ConsumeContext<GetUsers> context)
    {
        logger.LogDebug("Consuming {Request} username='{UserName}'", nameof(GetUsers), context.Message.UserName);

        if (context.Message.UserName.HasValue)
        {
            var suiteUser = await userManager.FindByNameAsync(context.Message.UserName.Value.Value);
            if (suiteUser is null)
                return new GetUsersResponse([], new ErrorInfo(UserErrorEvent.NotFound, $"Could not find user '{context.Message.UserName}'"));
            var user = await suiteUser.CreateUserProfile(userManager);
            return new GetUsersResponse([user]);
        }

        //Get
        var suiteUsers = await userManager.Users.ToListAsync(context.CancellationToken);
        var users = await Task.WhenAll(suiteUsers.Select(async u => await u.CreateUserProfile(userManager)));

        return new GetUsersResponse([.. users]);
    }

    protected override Task<GetUsersResponse> HandleException(ConsumeContext<GetUsers> context, Exception e)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetUsers));

        return Task.FromResult(new GetUsersResponse([], new ErrorInfo(UserErrorEvent.UnknownError, e.Message)));
    }
}
