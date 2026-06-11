using Core.Shared.Passkeys;
using Core.Shared.Passkeys.Contracts;
using Core.Shared.Passkeys.Requests;
using Core.Shared.UserManagement.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace Blazor.Server.Backend.Passkeys;

public sealed partial class GetPasskeysConsumer(
    UserManager<SuiteUser> userManager,
    ILogger<GetPasskeysConsumer> logger) :
    IConsumer<GetPasskeys>
{
    public async Task Consume(ConsumeContext<GetPasskeys> context)
    {
        try
        {
            var user = await userManager.FindByIdAsync(context.Message.UserId);
            if (user is null)
            {
                await context.RespondAsync(new GetPasskeysResponse([], new ErrorInfo((int)PasskeyError.UserNotFound, null)));
                return;
            }

            var passkeys = await userManager.GetPasskeysAsync(user);
            var dtos = passkeys
                .Select(p => new PasskeyInfo(PasskeyIdConverter.EncodePasskeyId(p.CredentialId), p.Name))
                .ToList();
            await context.RespondAsync(new GetPasskeysResponse(dtos));
        }
        catch (Exception e)
        {
            LogRequestError(e);
            await context.RespondAsync(new GetPasskeysResponse([], new ErrorInfo((int)PasskeyError.UnknownError, e.Message)));
        }
    }

    [LoggerMessage(LogLevel.Error, "Failed to get passkeys")]
    partial void LogRequestError(Exception exception);
}
