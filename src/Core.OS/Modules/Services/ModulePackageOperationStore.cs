using System.IO.Abstractions;
using System.Text.Json;
using Core.Module;
using Core.OS.Hosting;
using Core.OS.Instance;
using Core.OS.Modules.Extensions;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.Options;
using Sdk.Messaging;

namespace Core.OS.Modules.Services;


public partial class ModulePackageOperationStore(
    IFileSystem fileSystem,
    IModulePackageManifestStore packageStore,
    IModuleArtifactRepository moduleRepository,
    IOptions<InstanceOptions> instanceOptions,
    ILogger<ModulePackageOperationStore> logger) : IModulePackageOperationStore, IDisposable
{
    private readonly SemaphoreSlim _enqueueLock = new(1, 1);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _enqueueLock.Dispose();
    }
    public async Task<IReadOnlyList<ModulePackageChange>> EnqueueOperations(List<ModulePackageOperation> operations, CancellationToken cancellationToken)
    {
        await _enqueueLock.WaitAsync(cancellationToken);
        try
        {
            if (operations.Count == 0)
            {
                LogNoPackageUpdatesToQueue(logger);
                return [];
            }

            var result = await ProcessIncomingOperations(operations, cancellationToken);

            await AddPackageOptions(result, cancellationToken);

            var updateFile = fileSystem.GetModulePackageOperationsFilePath(instanceOptions.Value);
            if (result.Operations.Count == 0)
            {
                LogNoPendingOperations(logger, updateFile);
                fileSystem.File.Delete(updateFile);
                return result.Changes;
            }

            LogEnqueuedOperations(logger, result.Operations.Count, updateFile);

            // Rewrite file containing queued updates
            await using var fileStream = fileSystem.FileStream.New(updateFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fileStream, result.Operations, DefaultJsonSerializerSettings.Default, cancellationToken);
            return result.Changes;
        }
        finally
        {
            _enqueueLock.Release();
        }
    }

    private async Task<PackageChangeResult> ProcessIncomingOperations(List<ModulePackageOperation> operations, CancellationToken cancellationToken)
    {
        // Keep previous updates if no restart was performed meanwhile
        var previousUpdates = await DeserializeOperations(fileSystem, instanceOptions.Value, cancellationToken);
        var currentManifest = await packageStore.Load(cancellationToken);
        var changes = new List<ModulePackageChange>();

        // Overwrite already queued packages with new versions
        var updatedPackages = operations
                .UnionBy(previousUpdates, cp => cp.Package.Name)
                .Where(updatedPackage =>
                {
                    // It might happen that a package gets uninstalled and afterwards installed again with
                    // same version that is already in the packages file. In this case we can remove the operation
                    var currentDependency = currentManifest.Packages.FirstOrDefault(cd => cd.Name == updatedPackage.Package.Name);
                    if (updatedPackage.OperationKind == ModulePackageOperationKind.Uninstall && currentDependency is not null
                        && currentDependency.Version != updatedPackage.Package.Version)
                    {
                        LogRemovingUninstallOperation(logger, updatedPackage.Package.Name, updatedPackage.Package.Version);

                        changes.Add(new ModulePackageChange(CrudAction.Deleted, updatedPackage));
                        return false;
                    }

                    // If the package is already installed with the same version we can also remove the install operation from the queue
                    if (updatedPackage.OperationKind == ModulePackageOperationKind.Install && currentDependency?.Version == updatedPackage.Package.Version)
                    {
                        LogRemovingInstallOperation(logger, updatedPackage.Package.Name, updatedPackage.Package.Version);

                        changes.Add(new ModulePackageChange(CrudAction.Deleted, updatedPackage));
                        return false;
                    }

                    return true;
                })
                .OrderBy(p => p.Package.Name)
                .ToList();

        changes.AddRange(GetModulePackageChanges(previousUpdates, updatedPackages));

        return new(updatedPackages, changes);
    }

    private async Task AddPackageOptions(PackageChangeResult changeSet, CancellationToken cancellationToken)
    {
        // When a module gets upgraded we need to check for it's metadata to determine it's migration settings.
        // We add it to the operations to allow processing the operations without additional tasks to be done on startup.
        var packageUpdates = changeSet.Changes.Where(k => k.Action == CrudAction.Updated).ToList();
        if (packageUpdates.Count == 0)
            return;

        foreach (var update in packageUpdates)
        {
            LogEvaluatingPackageOptions(logger, update.Operation.Package.Name, update.PreviousVersion, update.Operation.Package.Version);

            if (update.PreviousVersion is null)
                throw new InvalidOperationException("Previous version is missing");

            var operation = changeSet.Operations.First(k => k.Package.Name == update.Operation.Package.Name);
            var metadata = await moduleRepository.GetModuleMetadata(operation.Package, cancellationToken)
                ?? throw new InvalidOperationException("Failed to download metadata for package");

            var isPatchUpdate = SuiteVersionUtils.IsPatchUpdate(update.PreviousVersion, operation.Package.Version);
            operation.Options = new ModulePackageOperationOptions(metadata.AutonomousMigration, isPatchUpdate);
        }
    }

    private record PackageChangeResult(List<ModulePackageOperation> Operations, List<ModulePackageChange> Changes);

    private static IEnumerable<ModulePackageChange> GetModulePackageChanges(List<ModulePackageOperation> previous, List<ModulePackageOperation> next)
    {
        var result = new List<ModulePackageChange>();

        foreach (var prev in previous)
        {
            var nextOp = next.FirstOrDefault(n => n.Package.Name == prev.Package.Name);
            if (nextOp is null)
            {
                result.Add(new ModulePackageChange(CrudAction.Deleted, prev));
                continue;
            }

            if (nextOp.OperationKind != prev.OperationKind || nextOp.Package.Version != prev.Package.Version)
            {
                result.Add(new ModulePackageChange(CrudAction.Updated, nextOp, prev.Package.Version));
            }
        }

        var added = next.Where(n => !previous.Any(p => p.Package.Name == n.Package.Name))
            .Select(n => new ModulePackageChange(CrudAction.Created, n));

        return result.Concat(added);
    }

    public async Task<IReadOnlyCollection<ModulePackageOperation>> GetEnqueuedOperations(CancellationToken cancellationToken)
    {
        try
        {
            return await DeserializeOperations(fileSystem, instanceOptions.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            LogGetEnqueuedOperationsError(logger, ex);
        }

        return [];
    }

    public static async Task<IReadOnlyCollection<ModulePackageOperation>> GetEnqueuedOperations(IFileSystem fs, InstanceOptions options, CancellationToken cancellationToken)
        => await DeserializeOperations(fs, options, cancellationToken);

    private static async Task<List<ModulePackageOperation>> DeserializeOperations(IFileSystem fs, InstanceOptions options, CancellationToken cancellationToken)
    {
        var updateFile = fs.GetModulePackageOperationsFilePath(options);
        if (!fs.File.Exists(updateFile))
            return [];

        await using var updateFileStream = fs.FileStream.New(updateFile, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });

        return await JsonSerializer.DeserializeAsync<List<ModulePackageOperation>>(updateFileStream, DefaultJsonSerializerSettings.Default, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize queued package operations from '{updateFile}'");
    }

    public void ClearEnqueuedOperations()
    {
        try
        {
            ClearEnqueuedOperationsInternal(fileSystem, instanceOptions.Value);
            LogClearedOperations(logger);
        }
        catch (Exception)
        {
            var operationsFilename = fileSystem.GetModulePackageOperationsFilePath(instanceOptions.Value);
            LogClearOperationsError(logger, operationsFilename);
        }
    }

    public static void ClearEnqueuedOperations(IFileSystem fs, InstanceOptions options)
        => ClearEnqueuedOperationsInternal(fs, options);

    private static void ClearEnqueuedOperationsInternal(IFileSystem fs, InstanceOptions options)
    {
        var sentinelFilename = fs.GetModulePackageOperationsSentinelFilePath(options);
        if (fs.File.Exists(sentinelFilename))
            fs.File.Delete(sentinelFilename);

        var operationsFilename = fs.GetModulePackageOperationsFilePath(options);
        if (!fs.File.Exists(operationsFilename))
            return;

        fs.File.Delete(operationsFilename);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No package updates to queue")]
    private static partial void LogNoPackageUpdatesToQueue(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No package operations to queue, deleting existing operations file '{OperationsFile}'")]
    private static partial void LogNoPendingOperations(ILogger logger, string operationsFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued {Count} package operations to '{OperationsFile}'")]
    private static partial void LogEnqueuedOperations(ILogger logger, int count, string operationsFile);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removing uninstall operation from queue because '{PackageName}' version '{PackageVersion}' it's not installed yet")]
    private static partial void LogRemovingUninstallOperation(ILogger logger, string packageName, string packageVersion);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removing install operation from queue because '{PackageName}' version '{PackageVersion}' is already installed")]
    private static partial void LogRemovingInstallOperation(ILogger logger, string packageName, string packageVersion);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Evaluating package options for '{PackageName}' update from version '{PreviousVersion}' to version '{NewVersion}'")]
    private static partial void LogEvaluatingPackageOptions(ILogger logger, string packageName, string? previousVersion, string newVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get enqueued package operations from file, returning empty collection")]
    private static partial void LogGetEnqueuedOperationsError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cleared enqueued package operations")]
    private static partial void LogClearedOperations(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete queued package operations file '{OperationsFile}'")]
    private static partial void LogClearOperationsError(ILogger logger, string operationsFile);
}
