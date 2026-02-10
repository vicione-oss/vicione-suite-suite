using System.Security.Authentication;
using Core.OS.Mail;
using Core.OS.Mail.MailKit;
using Core.Shared.Mail;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using Xunit;
using AuthenticationException = MailKit.Security.AuthenticationException;

namespace Core.OS.Tests.Mail;

public class MailingTests
{
    private const string ExistingUser = "Eddy";
    private const string CorrectPassword = "Up2noGood!!!";
    private const string TestServer = "mailpit.infra.ifm-sw.net";
    private const int ServerPort = 1025;

    [Fact]
    [Trait("Category", Traits.Integration)]
    public async Task Sending_mail_with_correct_password_works_as_expected()
    {
        // Arrange
        var sender = CreateMailkitMailSender(CorrectPassword);
        var email = CreateMessage(to: "mail-alert@localhost",
            subject: "Test",
            message: $"""
                      <html><div>This message was sent by test <em>{nameof(Sending_mail_with_correct_password_works_as_expected)}</em></div></html>
                      """);

        // Act + Assert
        await sender.SendMail(email, TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", Traits.Integration)]
    public async Task Sending_mail_with_incorrect_password_fails_as_expected()
    {
        // Arrange
        var sender = CreateMailkitMailSender(CorrectPassword.ToUpperInvariant());
        var email = CreateMessage(to: "admin@localhost",
            subject: "Test",
            message: "This message should never be sent, as authentication is supposed to fail.");

        // Act + Assert
        await Assert.ThrowsAnyAsync<AuthenticationException>(() => sender.SendMail(email, TestContext.Current.CancellationToken));
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
        string serverAddress = TestServer)
    {
        var smtpMailOptions = new SmtpMailOptions
        {
            FromAddress = "Integration@localhost",
            FromUserName = fromUserName,
            Password = password,
            ServerAddress = serverAddress,
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
