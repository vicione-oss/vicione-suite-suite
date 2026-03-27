using Core.Shared.HostManagement.Commands;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Events;

[ForwardToUI]
public record ControlSystemCompleted(SystemCommand Command) : ResponseEventBase
{
    public const int UnknownError = -1;
}
