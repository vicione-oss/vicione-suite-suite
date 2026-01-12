using Core.Shared.Instance.Commands;
using Sdk.Messaging;

namespace Core.Shared.Instance.Events;

[ForwardToUI]
public sealed record ControlInstanceCompleted(Guid InstanceId, InstanceCommand Command) : IEvent;
