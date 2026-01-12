using MailKit.Net.Smtp;

namespace Core.OS.Mail.MailKit;

public interface IMailClientConfigurator
{
    public void ConfigureSmtpClient(SmtpClient smtpClient);
}
