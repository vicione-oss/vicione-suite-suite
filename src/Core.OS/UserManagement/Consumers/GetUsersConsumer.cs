using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class GetUsersConsumer(UserManager<SuiteUser> userManager, ILogger<GetUsersConsumer> logger)
    : RequestConsumer<GetUsers, GetUsersResponse>
{
    public override async Task<GetUsersResponse> Respond(GetUsers message, CancellationToken cancellationToken)
    {
        if (message.UserName?.Value is not null)
        {
            var normalizedUserName = userManager.NormalizeName(message.UserName.Value.Value);
            var suiteUser = await userManager.FindByNormalizedUser(normalizedUserName, cancellationToken);

            if (suiteUser is null)
                return new GetUsersResponse([], new ErrorInfo(UserErrorCodes.NotFound, $"Could not find user '{message.UserName}'"));

            var user = await suiteUser.CreateUserProfile(userManager);
            return new GetUsersResponse([user]);
        }

        //Get
        var suiteUsers = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var users = await CreateUserProfilesThreadSafe(suiteUsers);

        return new GetUsersResponse([.. users]);
    }

    private async Task<List<UserProfile>> CreateUserProfilesThreadSafe(List<SuiteUser> suiteUsers)
    {
        // Build the profiles sequentially: CreateUserProfile queries the UserManager's DbContext
        // (roles + claims), and a DbContext is not thread-safe — running these concurrently with
        // Task.WhenAll triggers "A second operation was started on this context instance".
        var users = new List<UserProfile>(suiteUsers.Count);
        foreach (var suiteUser in suiteUsers)
            users.Add(await suiteUser.CreateUserProfile(userManager));
        return users;
    }

    public override Task<GetUsersResponse> HandleException(GetUsers message, Exception e, CancellationToken cancellationToken)
    {
        LogRequestError(logger, e);

        return Task.FromResult(new GetUsersResponse([], new ErrorInfo(UserErrorCodes.UnknownError, e.Message)));
    }

    [LoggerMessage(LogLevel.Error, "Failed to get users")]
    private static partial void LogRequestError(ILogger<GetUsersConsumer> logger, Exception exception);
}
