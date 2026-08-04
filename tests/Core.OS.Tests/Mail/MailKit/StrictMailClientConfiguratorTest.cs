using System.Security.Authentication;
using Core.OS.Mail.MailKit;
using MailKit.Net.Smtp;

namespace Core.OS.Tests.Mail.MailKit;

public class StrictMailClientConfiguratorTest
{
    [Fact]
    public void Should_set_protocols_to_latest_tls()
    {
        // Arrange
        var configurator = new StrictMailClientConfigurator();
        using var smtpClient = new SmtpClient();

        // Act
        configurator.ConfigureSmtpClient(smtpClient);

        // Assert
        smtpClient.SslProtocols.Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
    }
}
