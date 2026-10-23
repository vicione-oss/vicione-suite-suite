using Sdk.Messaging;

namespace Core.Shared.Mail.Requests;

public sealed record GetSmtpSettings : IInstanceDependentRequest<GetSmtpSettingsResponse>;
