using Sdk.Messaging;

namespace Core.Shared.HostManagement.Commands;

public enum SystemCommand
{
    Reset,
    Restart
}

public record ControlSystem(SystemCommand Command) : IInstanceDependentCommand
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}
