using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Messaging;

namespace Core.Shared.Instance.HealthCheck;

[ForwardToUI]
public sealed record InstanceHealthChangedEvent(HealthStatus Status, Guid InstanceId) : IEvent;
