using Sdk.Messaging;

namespace Core.Shared.EnvironmentOverrides.Commands;

public sealed record SetEnvironmentOverrides(Dictionary<string, string> Overrides) : IInstanceDependentCommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
