using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.MessageBus.Extensions;
using Core.OS.Modules.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Contracts;
using MassTransit;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Modules.Consumers;

/// <summary>
/// Converges a slave to the cluster's desired module set. Compares the desired packages the master sends
/// against the local manifest; when they differ it enqueues the required install/uninstall operations and
/// restarts the node so the operations are applied on the next startup. A signature guard ensures at most one
/// restart per distinct desired set, preventing a restart loop if a target can never be satisfied.
/// </summary>
[ReadOnlyConsumer]
public sealed partial class ReconcileModuleManifestConsumer(
    IModulePackageManifestStore manifestStore,
    IModulePackageOperationStore operationStore,
    IFileSystem fileSystem,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<ReconcileModuleManifestConsumer> logger) : IConsumer<ReconcileModuleManifest>
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);

    public async Task Consume(ConsumeContext<ReconcileModuleManifest> context)
    {
        // Reconciliation only applies to slaves. Master and standalone instances are the source of truth for
        // their own module set and never receive this command in practice.
        if (instanceOptions.Value.Type != InstanceType.Slave)
            return;

        var options = instanceOptions.Value;
        var desired = context.Message.DesiredPackages;
        var instanceId = context.Message.InstanceId;

        var manifest = await manifestStore.Load(context.CancellationToken);
        var operations = DetermineOperations(manifest.Packages, desired);

        var signaturePath = fileSystem.GetModuleReconcileSignatureFilePath(options);

        if (operations.Count == 0)
        {
            // Already converged: drop any previous attempt marker so a future change can reconcile again.
            if (fileSystem.File.Exists(signaturePath))
                fileSystem.File.Delete(signaturePath);

            LogAlreadyConverged(logger, instanceId, manifest.Packages.Count);
            return;
        }

        var signature = BuildSignature(desired);
        if (fileSystem.File.Exists(signaturePath)
            && string.Equals(await fileSystem.File.ReadAllTextAsync(signaturePath, context.CancellationToken), signature, StringComparison.Ordinal))
        {
            // We already enqueued and restarted for this exact desired set but it still does not match.
            // Stop here to avoid a restart loop for an unsatisfiable target.
            LogReconcileLoopGuard(logger, instanceId, operations.Count);
            return;
        }

        await operationStore.EnqueueOperations(operations, context.CancellationToken);

        var restart = new ControlInstance
        {
            InstanceId = instanceId,
            Action = InstanceCommand.Restart,
            Delay = RestartDelay,
            CorrelationId = context.Message.CorrelationId,
            Reason = "Module manifest reconciliation",
        };
        await context.SendToInstance(restart, instanceId, context.CancellationToken);

        // Record the attempt last so a transient failure above is retried instead of being suppressed by the guard.
        await fileSystem.File.WriteAllTextAsync(signaturePath, signature, context.CancellationToken);

        LogReconciling(logger, instanceId, operations.Count);
    }

    private static List<ModulePackageOperation> DetermineOperations(IReadOnlyCollection<ModuleDependencyPackage> local, IReadOnlyCollection<ModuleDependencyPackage> desired)
    {
        var localNames = local.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var desiredNames = desired.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        // Identity-based diff: install desired modules the node is missing and uninstall modules it should no
        // longer have. Version drift for modules present on both sides is intentionally ignored (the cluster
        // is assumed to run a homogeneous module version set).
        var installs = desired
            .Where(p => !localNames.Contains(p.Name))
            .Select(p => new ModulePackageOperation(p, ModulePackageOperationKind.Install));

        var uninstalls = local
            .Where(p => !desiredNames.Contains(p.Name))
            .Select(p => new ModulePackageOperation(p, ModulePackageOperationKind.Uninstall));

        return [.. installs, .. uninstalls];
    }

    private static string BuildSignature(IEnumerable<ModuleDependencyPackage> packages)
        => string.Join(";", packages.Select(p => $"{p.Name}@{p.Version}").OrderBy(s => s, StringComparer.Ordinal));

    [LoggerMessage(Level = LogLevel.Debug, Message = "Module manifest for instance {InstanceId} already matches the cluster ({PackageCount} package(s))")]
    private static partial void LogAlreadyConverged(ILogger<ReconcileModuleManifestConsumer> logger, Guid instanceId, int packageCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconciling module manifest for instance {InstanceId}: enqueued {OperationCount} operation(s), restarting to apply")]
    private static partial void LogReconciling(ILogger<ReconcileModuleManifestConsumer> logger, Guid instanceId, int operationCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping module manifest reconciliation for instance {InstanceId}: {OperationCount} operation(s) still pending after a previous attempt for the same desired set — avoiding a restart loop")]
    private static partial void LogReconcileLoopGuard(ILogger<ReconcileModuleManifestConsumer> logger, Guid instanceId, int operationCount);
}
