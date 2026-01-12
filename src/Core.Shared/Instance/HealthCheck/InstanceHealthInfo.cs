using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Messaging;

namespace Core.Shared.Instance.HealthCheck;

[ForwardToUI]
public sealed record InstanceHealthInfo(Guid SenderInstanceId, DateTime WhenSentUtc, HealthStatus Status) : IEvent;
