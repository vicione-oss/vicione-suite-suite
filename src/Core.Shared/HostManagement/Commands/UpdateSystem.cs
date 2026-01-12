using Sdk.Messaging;

namespace Core.Shared.HostManagement.Commands;

public record UpdateSystem(string FilePath) : IInstanceDependentCommand
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
