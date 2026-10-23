using Core.Shared.Mail;
using Core.Shared.Mail.Contracts;
using Core.Shared.Mail.Requests;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;

namespace Core.OS.Mail.Consumers;

/// <summary>
/// Reads the same <see cref="IOptions{TOptions}"/> as the mail sender and its <see cref="IMailSenderStatus"/>, so the
/// values and the status in one answer always describe the settings mail is actually sent with.
/// </summary>
public sealed partial class GetSmtpSettingsConsumer(
    IOptions<SmtpMailOptions> options,
    IMailSenderStatus mailSenderStatus,
    ILogger<GetSmtpSettingsConsumer> logger)
    : InstanceDependentRequestConsumer<GetSmtpSettings, GetSmtpSettingsResponse>
{
    public override Task<GetSmtpSettingsResponse> Respond(GetSmtpSettings message, CancellationToken cancellationToken)
    {
        var smtpOptions = options.Value;

        var settings = HasNoValues(smtpOptions)
            ? null
            : new SmtpSettings(
                smtpOptions.ServerAddress,
                smtpOptions.ServerPort,
                smtpOptions.UserName,
                smtpOptions.FromAddress,
                smtpOptions.FromName,
                PasswordSet: !string.IsNullOrWhiteSpace(smtpOptions.Password),
                MailSendingEnabled: mailSenderStatus.IsConfigured());

        return Task.FromResult(new GetSmtpSettingsResponse(settings));
    }

    public override Task<GetSmtpSettingsResponse> HandleException(GetSmtpSettings message, Exception e, CancellationToken cancellationToken)
    {
        LogFailedToGetSmtpSettings(logger, e);

        return Task.FromResult(new GetSmtpSettingsResponse(null, new(MailErrorCodes.GetSmtpSettingsFailed, e.Message)));
    }

    /// <summary>
    /// Binding turns a missing <c>Smtp</c> section into default values, so "no section" and "a section without values" look alike.
    /// </summary>
    private static bool HasNoValues(SmtpMailOptions smtpOptions)
        => smtpOptions.ServerPort == 0
           && string.IsNullOrWhiteSpace(smtpOptions.ServerAddress)
           && string.IsNullOrWhiteSpace(smtpOptions.UserName)
           && string.IsNullOrWhiteSpace(smtpOptions.FromAddress)
           && string.IsNullOrWhiteSpace(smtpOptions.FromName)
           && string.IsNullOrWhiteSpace(smtpOptions.Password);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get SMTP settings")]
    private static partial void LogFailedToGetSmtpSettings(ILogger<GetSmtpSettingsConsumer> logger, Exception exception);
}
