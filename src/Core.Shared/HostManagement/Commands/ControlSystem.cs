using Sdk.Messaging;

namespace Core.Shared.HostManagement.Commands;

public enum SystemCommand
{
    Reset,
    Restart,
    Shutdown
}

public record ControlSystem(SystemCommand Command) : IInstanceDependentCommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
