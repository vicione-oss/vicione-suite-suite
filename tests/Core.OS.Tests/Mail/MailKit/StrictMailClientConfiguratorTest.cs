using System.Security.Authentication;
using Core.OS.Mail.MailKit;
using MailKit.Net.Smtp;
using Xunit;

namespace Core.OS.Tests.Mail.MailKit;

public class StrictMailClientConfiguratorTest
{
    [Fact]
    public void Configuring_will_always_set_the_protocols_to_latest_Tls()
    {
        // Arrange
        var configurator = new StrictMailClientConfigurator();
        using SmtpClient smtpClient = new SmtpClient();

        // Act
        configurator.ConfigureSmtpClient(smtpClient);

        // Assert
        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, smtpClient.SslProtocols);
    }
}
