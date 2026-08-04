using System.Security.Authentication;
using Core.OS.Mail;
using Core.OS.Mail.MailKit;
using Core.Shared.Mail;
using Core.Tests.Tools;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using AuthenticationException = MailKit.Security.AuthenticationException;

namespace Core.OS.Tests.Mail;

public class MailingTests
{
    // Credentials and SMTP target are resolved from the environment so CI can run the
    // tests against the mailpit instance it provides. The defaults match a mailpit
    // started locally (see docs/integration-testing.md).
    private static readonly string ExistingUser
        = IntegrationServiceSettings.GetValue("MAILPIT_SMTP_USERNAME", "smtp-tester");

    private static readonly string CorrectPassword
        = IntegrationServiceSettings.GetValue("MAILPIT_SMTP_PASSWORD", "MailpitTest123!");

    // Deliberately wrong: appending guarantees a mismatch regardless of the configured password.
    private static readonly string IncorrectPassword = $"{CorrectPassword}-invalid";

    private static readonly string TestServer = IntegrationServiceSettings.GetHost("MAILPIT_SMTP_HOST");
    private static readonly int ServerPort = IntegrationServiceSettings.GetPort("MAILPIT_SMTP_PORT", 1025);

    // mailpit HTTP API, used to verify the delivered message. Defaults to the local web UI port.
    private static readonly string MailpitApiUrl
        = IntegrationServiceSettings.GetValue("MAILPIT_API_URL", "http://localhost:8025");

    public sealed class SendMail : MailingTests
    {
        [Fact]
        [Trait("Category", Traits.Integration)]
        public async Task Should_deliver_mail_when_password_is_correct()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;
            var mailpit = new MailpitClient(new Uri(MailpitApiUrl));

            var sender = CreateMailkitMailSender(CorrectPassword);
            const string recipient = "mail-alert@localhost";
            const string subject = "Test";
            var uniqueMessageIdentifier = Guid.NewGuid().ToString();
            var body = $"""
                        <html><div>This message was sent by test <em>{uniqueMessageIdentifier}</em></div></html>
                        """;
            var email = CreateMessage(to: recipient, subject: subject, message: body);

            // Act
            await sender.SendMail(email, cancellationToken);

            // Assert - mailpit received exactly the message we sent, located by its unique identifier.
            var matches = await mailpit.SearchMessages(uniqueMessageIdentifier, cancellationToken);
            var received = matches.Should().ContainSingle().Subject;
            received.Subject.Should().Be(subject);
            received.To.Should().ContainSingle().Which.Address.Should().Be(recipient);
            received.From?.Address.Should().Be("Integration@localhost");
        }

        [Fact]
        [Trait("Category", Traits.Integration)]
        public async Task Should_throw_when_password_is_incorrect()
        {
            // Arrange
            var sender = CreateMailkitMailSender(IncorrectPassword);
            var email = CreateMessage(to: "admin@localhost",
                subject: "Test",
                message: "This message should never be sent, as authentication is supposed to fail.");

            // Act + Assert
            var act = () => sender.SendMail(email, TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<AuthenticationException>();
        }
    }

    private static MailkitMailSender CreateMailkitMailSender(string password)
    {
        var smtpMailOptions = CreateSmtpOptions(ExistingUser, password);
        var sender = new MailkitMailSender(Options.Create(smtpMailOptions),
            new SelfSignedCertificateAcceptingConfigurator());
        return sender;
    }

    private static SmtpMailOptions CreateSmtpOptions(string fromUserName,
        string password,
        string? serverAddress = null)
    {
        var smtpMailOptions = new SmtpMailOptions
        {
            FromAddress = "Integration@localhost",
            FromUserName = fromUserName,
            Password = password,
            ServerAddress = serverAddress ?? TestServer,
            ServerPort = ServerPort
        };
        return smtpMailOptions;
    }

    private static Message CreateMessage(string to, string subject, string? message)
        => new()
        {
            RecipientAddress = to,
            Subject = subject,
            Body = message,
        };
}

public class SelfSignedCertificateAcceptingConfigurator : IMailClientConfigurator
{
    public void ConfigureSmtpClient(SmtpClient smtpClient)
    {
        smtpClient.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        // ⚠️ Insecure and should not be used in production,
        // but required for self-signed certs in test environment.
#pragma warning disable CA5359
        smtpClient.ServerCertificateValidationCallback = (s, c, h, e) => true;
#pragma warning restore CA5359
    }
}
