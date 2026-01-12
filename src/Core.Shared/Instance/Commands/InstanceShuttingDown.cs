using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

[ForwardToUI]
public sealed record InstanceShuttingDown(Guid InstanceId, DateTimeOffset ShutdownTime) : IEvent;
