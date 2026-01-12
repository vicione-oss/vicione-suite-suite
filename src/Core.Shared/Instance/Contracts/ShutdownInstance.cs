using Sdk.Messaging;

namespace Core.Shared.Instance.Contracts;

public sealed class ShutdownInstance : IInstanceDependentCommand
{
    public required Guid InstanceId { get; init; }
    public required string Reason { get; init; }
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(300);
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
