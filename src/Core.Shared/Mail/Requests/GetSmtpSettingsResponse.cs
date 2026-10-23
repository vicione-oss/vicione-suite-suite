using Core.Shared.Mail.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Mail.Requests;

/// <param name="Settings"><c>null</c> when none of the instance's SMTP settings is set.</param>
/// <param name="RequestError">Set when the settings could not be read.</param>
public sealed record GetSmtpSettingsResponse(SmtpSettings? Settings, ErrorInfo? RequestError = null) : IResponse;
