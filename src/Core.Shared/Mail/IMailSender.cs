namespace Core.Shared.Mail;

public interface IMailSender
{
    /// <summary>
    /// Send a message to a single recipient.
    ///
    /// Will throw an exception if underlying transport is failing.
    /// </summary>
    /// <returns>if no exception was thrown mail was successfully sent.</returns>
    Task SendMail(Message message, CancellationToken cancellationToken = default);
}
