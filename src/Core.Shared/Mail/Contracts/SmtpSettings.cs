namespace Core.Shared.Mail.Contracts;

/// <summary>
/// The SMTP settings an instance holds, plus its <see cref="IMailSenderStatus.IsConfigured"/> result. Of the password
/// it carries only whether one is set: the password itself must never leave the instance.
/// </summary>
public sealed record SmtpSettings(
    string? ServerAddress,
    int ServerPort,
    string? UserName,
    string? FromAddress,
    string? FromName,
    bool PasswordSet,
    bool MailSendingEnabled);
