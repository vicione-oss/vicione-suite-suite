using Core.OS.UserManagement.Extensions;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;
using Sdk.UserManagement.Requests;

namespace Core.OS.UserManagement.Consumers;

public sealed partial class GetUserInformationConsumer(UserManager<SuiteUser> userManager, ILogger<GetUserInformationConsumer> logger)
    : RequestConsumer<GetUserInformation, GetUserInformationResponse>
{
    public override async Task<GetUserInformationResponse> Respond(GetUserInformation message, CancellationToken cancellationToken)
    {
        if (message.UserName is not null)
        {
            var normalizedUserName = userManager.NormalizeName(message.UserName);
            var suiteUser = await userManager.FindByNormalizedUser(normalizedUserName, cancellationToken);

            if (suiteUser is null)
                return new GetUserInformationResponse([], new ErrorInfo(UserErrorCodes.NotFound, $"Could not find user '{message.UserName}'"));

            var user = suiteUser.ToUserInformation();
            return new GetUserInformationResponse([user]);
        }

        //Get
        var suiteUsers = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);

        return new GetUserInformationResponse([.. suiteUsers.Select(u => u.ToUserInformation())]);
    }

    public override Task<GetUserInformationResponse> HandleException(GetUserInformation message, Exception e, CancellationToken cancellationToken)
    {
        LogRequestError(logger, e);

        return Task.FromResult(new GetUserInformationResponse([], new ErrorInfo(UserErrorCodes.UnknownError, e.Message)));
    }

    [LoggerMessage(LogLevel.Error, "Failed to get users")]
    private static partial void LogRequestError(ILogger<GetUserInformationConsumer> logger, Exception exception);
}
