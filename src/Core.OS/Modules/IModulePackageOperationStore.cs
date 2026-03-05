using Core.Shared.Modules.Contracts;

namespace Core.OS.Modules;

public interface IModulePackageOperationStore
{
    /// <summary>
    /// Queues cluster dependency operations that will be merged into storage on the next call of <see cref="ProcessQueuedOperations(CancellationToken)"/>.
    /// </summary>
    Task<IReadOnlyList<ModulePackageChange>> EnqueueOperations(List<ModulePackageOperation> operations, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the currently enqueued operations.
    /// </summary>
    Task<IReadOnlyCollection<ModulePackageOperation>> GetEnqueuedOperations(CancellationToken cancellationToken);

    /// <summary>
    /// Removes all currently enqueued operations.
    /// </summary>
    void ClearEnqueuedOperations();
}
