using Core.Shared.Instance.Commands;
using Sdk.Messaging;

namespace Core.Shared.Instance.Events;

[ForwardToUI]
public sealed record ControlInstanceError(Guid InstanceId, InstanceCommand Command, ErrorInfo Error) : IEvent;
