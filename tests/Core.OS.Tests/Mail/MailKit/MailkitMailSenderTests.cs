using Core.OS.Mail;
using Core.OS.Mail.MailKit;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace Core.OS.Tests.Mail.MailKit;

public class MailkitMailSenderTests
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

    public sealed class ToFromAddress
    {
        [Fact]
        public void Should_put_the_from_name_in_front_of_the_address()
        {
            // Act
            var fromAddress = MailkitMailSender.ToFromAddress("ViciOne Suite", "noreply@corp.example");

            // Assert
            fromAddress.ToString().Should().Be("\"ViciOne Suite\" <noreply@corp.example>");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Should_leave_out_the_display_name_when_no_from_name_is_set(string? fromName)
        {
            // Act
            var fromAddress = MailkitMailSender.ToFromAddress(fromName, "noreply@corp.example");

            // Assert
            fromAddress.ToString().Should().Be("noreply@corp.example");
        }
    }
}
