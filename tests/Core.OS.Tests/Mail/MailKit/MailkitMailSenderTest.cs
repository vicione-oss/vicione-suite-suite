using Core.OS.Mail;
using Core.OS.Mail.MailKit;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.Mail.MailKit;

public class MailkitMailSenderTest
{
    [Fact]
    public void Our_mail_sender_offers_the_interface_required_by_identity()
    {
        Assert.IsType<IEmailSender>(new MailkitMailSender(Substitute.For<IOptions<SmtpMailOptions>>(),
                Substitute.For<IMailClientConfigurator>()),
            exactMatch: false);
    }
}
