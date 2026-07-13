using AwesomeAssertions;
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
    public void Should_implement_interface_required_by_identity()
    {
        // Arrange + Act
        var sender = new MailkitMailSender(Substitute.For<IOptions<SmtpMailOptions>>(),
            Substitute.For<IMailClientConfigurator>());

        // Assert
        sender.Should().BeAssignableTo<IEmailSender>();
    }
}
