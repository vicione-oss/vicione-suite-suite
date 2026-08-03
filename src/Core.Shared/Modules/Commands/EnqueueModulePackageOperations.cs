using Core.Shared.Modules.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Modules.Commands;

/// <summary>
/// Instance-dependent command that instructs a single node to enqueue module package operations
/// to its local operation store. Dispatched by <c>UpdateModulePackageOperationsConsumer</c> to every
/// instance in the cluster so all nodes apply the same module changes on their next restart.
/// </summary>
public sealed record EnqueueModulePackageOperations(List<ModulePackageOperation> Operations) : IInstanceDependentCommand
{
    public Guid InstanceId { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
