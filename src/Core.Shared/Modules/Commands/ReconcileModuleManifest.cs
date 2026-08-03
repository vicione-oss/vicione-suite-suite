using Sdk.Messaging;
using Sdk.Modules;

namespace Core.Shared.Modules.Commands;

/// <summary>
/// Instance-dependent command the master sends to a (re)joining slave carrying the cluster's desired module
/// package set. The receiving node compares it against its own local manifest and, if they differ, enqueues
/// the required install/uninstall operations and restarts to converge. This is how nodes that were offline
/// beyond the queue lifetime or are brand new catch up to the cluster module state.
/// </summary>
public sealed record ReconcileModuleManifest(List<ModuleDependencyPackage> DesiredPackages) : IInstanceDependentCommand
{
    public Guid InstanceId { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
