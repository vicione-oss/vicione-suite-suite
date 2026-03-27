using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record UpdateSystemStarted(string? Message, bool WithWarnings) : ResponseEventBase
{
    public const int UnknownError = -1;
}
