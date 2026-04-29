using System.IO.Abstractions;
using Core.OS.Extensions;
using Core.OS.Instance;
using Core.OS.Modules.Extensions;
using Core.Shared.Modules.Contracts;
using Microsoft.Extensions.Options;
using Sdk.Modules;
using Serilog;

namespace Core.OS.Modules.Services;

internal class ModulePackageOperationProcessor
{
    public static async Task<ModulePackageManifest> ApplyEnqueuedOperations(IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger, CancellationToken cancellationToken)
    {
        var manifest = await ModulePackageManifestStore.Load(fileSystem, options, logger, cancellationToken);

        var operations = await ModulePackageOperationStore.GetEnqueuedOperations(fileSystem, options, cancellationToken);
        if (operations.Count == 0)
        {
            // Remove any orphaned sentinel left by a crash after sentinel deletion but before ops file deletion
            var orphanedSentinel = fileSystem.GetModulePackageOperationsSentinelFilePath(options);
            if (fileSystem.File.Exists(orphanedSentinel))
                fileSystem.File.Delete(orphanedSentinel);

            return manifest;
        }

        var sentinelPath = fileSystem.GetModulePackageOperationsSentinelFilePath(options);
        if (!fileSystem.File.Exists(sentinelPath))
        {
            // First attempt: write sentinel before the destructive workspace cleanup so that a crash
            // here does not cause directories to be wiped a second time on the next startup.
            await fileSystem.File.WriteAllTextAsync(sentinelPath, string.Empty, cancellationToken);
            ProcessOperationOptions(operations, fileSystem, options, logger);
        }
        else
        {
            logger.Information("Skipping workspace cleanup for {Count} pending operation(s) — sentinel present, cleanup was already applied in a previous run", operations.Count);
        }

        manifest.Packages = DeterminePackageChanges(manifest, operations);

        await ModulePackageManifestStore.Store(manifest, fileSystem, options, cancellationToken);

        ModulePackageOperationStore.ClearEnqueuedOperations(fileSystem, options);

        return manifest;
    }

    private static void ProcessOperationOptions(IEnumerable<ModulePackageOperation> operations, IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger)
    {
        var operationsWithOptions = operations.Where(o => o.Options is not null).ToList();
        if (operationsWithOptions.Count == 0)
            return;

        using var loggerFactory = new LoggerFactory();
        loggerFactory.AddSerilog(logger);
        var workspaceLogger = loggerFactory.CreateLogger<WorkspaceManagement>();
        var workspaceManagement = new WorkspaceManagement(fileSystem, Options.Create(options), workspaceLogger);

        foreach (var operation in operationsWithOptions)
        {
            if (operation.Options is null)
                continue;

            var resetHome = !operation.Options.AutonomousMigration && !operation.Options.IsPatchUpdate;
            var resetCache = !operation.Options.IsPatchUpdate;

            logger.Information("Apply package update operation for module {PackageName}, resetHome: {ResetHome}, resetCache: {ResetCache}", operation.Package.Name, resetHome, resetCache);

            if (resetHome)
            {
                var moduleHome = workspaceManagement.GetHomeDirectory(operation.Package.Name);
                fileSystem.TryDeleteDirectory(moduleHome, operation.Package.Name, logger);
            }

            if (resetCache)
            {
                var moduleCache = workspaceManagement.GetCacheDirectory(operation.Package.Name);
                fileSystem.TryDeleteDirectory(moduleCache, operation.Package.Name, logger);
            }
        }
    }

    private static List<ModuleDependencyPackage> DeterminePackageChanges(ModulePackageManifest packagesManifest, IEnumerable<ModulePackageOperation> operations)
    {
        // Get the packages to be removed
        var packagesToRemove = operations
            .Where(k => k.OperationKind == ModulePackageOperationKind.Uninstall)
            .Select(k => k.Package.Name)
            .ToHashSet();

        // Overwrite already queued packages with new versions
        var updatedPackages = operations
            .Where(k => k.OperationKind == ModulePackageOperationKind.Install)
            .Select(k => k.Package)
            .UnionBy(packagesManifest.Packages, cp => cp.Name)
            .Where(p => !packagesToRemove.Contains(p.Name))
            .OrderBy(p => p.Name)
            .ToList();

        return updatedPackages;
    }
}
