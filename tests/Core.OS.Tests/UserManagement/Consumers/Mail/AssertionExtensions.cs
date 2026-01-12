using Core.Shared.Mail;
using NSubstitute;

namespace Core.OS.Tests.UserManagement.Consumers.Mail;

public static class AssertionExtensions
{
    public static async Task AssertSentEmailContainsCallbackLink(this IMailSender mailSender, string callbackLink)
        => await mailSender
            .Received()
            .SendMail(Arg.Is<Message>(m => m.Body!.Contains(callbackLink)));
}
