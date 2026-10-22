using Core.OS.Mail.Extensions;
using Core.Shared.Mail;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace Core.OS.Mail.MailKit;

public sealed class MailkitMailSender(
    IOptions<SmtpMailOptions> options,
    IMailClientConfigurator mailClientConfigurator)
    : IMailSender, IEmailSender, IMailSenderStatus
{
    private const TextFormat SupportedFormat = TextFormat.Html;
    private const int SmtpTimeoutMilliseconds = 10_000;

    public async Task SendMail(Message message, CancellationToken cancellationToken = default)
    {
        using var email = CreateMimeMessage(message);

        await SendMimeMessage(email, cancellationToken);
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
        => SendMail(new Message
        {
            RecipientAddress = email,
            Subject = subject,
            Body = htmlMessage
        });

    private async Task SendMimeMessage(MimeMessage email, CancellationToken cancellationToken)
    {
        // No DisconnectAsync: a failing QUIT after delivery would retry and send the mail twice.
        using var smtpClient = new SmtpClient { Timeout = SmtpTimeoutMilliseconds };
        mailClientConfigurator.ConfigureSmtpClient(smtpClient);

        await ConnectToConfiguredSmtpServer(smtpClient,
            options.Value.ServerAddress,
            options.Value.ServerPort,
            cancellationToken);
        await AuthenticateConfiguredUser(smtpClient,
            options.Value.FromUserName,
            options.Value.Password,
            cancellationToken);

        await smtpClient.SendAsync(email, cancellationToken);
    }

    private static async Task ConnectToConfiguredSmtpServer(SmtpClient smtpClient,
        string serverAddress,
        int serverPort,
        CancellationToken cancellationToken)
    {
        await smtpClient.ConnectAsync(serverAddress,
            serverPort,
            SecureSocketOptions.StartTls,
            cancellationToken);
    }

    private static async Task AuthenticateConfiguredUser(SmtpClient smtpClient,
        string fromUserName,
        string password,
        CancellationToken cancellationToken)
        => await smtpClient.AuthenticateAsync(fromUserName,
            password,
            cancellationToken);

    private MimeMessage CreateMimeMessage(Message message)
    {
        var email = new MimeMessage();
        email.From.Add(ToFromAddress(options.Value.FromUserName,
            options.Value.FromAddress));
        email.To.Add(MailboxAddress.Parse(message.RecipientAddress));
        email.Subject = message.Subject;
        email.Body = new TextPart(SupportedFormat) { Text = message.Body ?? string.Empty };
        return email;
    }

    private static MailboxAddress ToFromAddress(string fromUserName, string fromAddress)
        => new(fromUserName, fromAddress);

    public bool IsConfigured() => options.Value.IsConfigured();
}
