using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

public sealed class ControlInstance : IInstanceDependentCommand
{
    public Guid InstanceId { get; set; }
    public required InstanceCommand Action { get; init; }
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public TimeSpan? Delay { get; set; }

    public string? Reason { get; set; }
}

public enum InstanceCommand
{
    Synchronize,
    Delete,
    Restart
}
