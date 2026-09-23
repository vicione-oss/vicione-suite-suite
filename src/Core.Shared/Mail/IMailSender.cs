namespace Core.Shared.Mail;

public interface IMailSender
{
    /// <summary>
    /// Sends a message to a single recipient. Throws when the underlying transport fails.
    /// </summary>
    Task SendMail(Message message, CancellationToken cancellationToken = default);
}
