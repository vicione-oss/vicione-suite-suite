using System.Security.Authentication;
using MailKit.Net.Smtp;

namespace Core.OS.Mail.MailKit;

internal sealed class StrictMailClientConfigurator : IMailClientConfigurator
{
    const SslProtocols OnlyUpToDateProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

    public void ConfigureSmtpClient(SmtpClient smtpClient)
    {
        smtpClient.SslProtocols = OnlyUpToDateProtocols;
    }
}
