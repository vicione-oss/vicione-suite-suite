using System.IO.Abstractions;
using Core.OS.Extensions;
using Core.OS.Hosting.Contracts;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Hosting;
using Sdk.Instance;

namespace Core.OS.Hosting.Extensions;

internal static partial class SuitePreparationPipelineExtensions
{
    public static SuitePreparationPipeline UseInstanceIdentification(
        this SuitePreparationPipeline pipeline)
        => pipeline.Use((context, _) =>
        {
            context.FileSystem.EnsureInstanceIdFile(context.InstanceOptions);

            var version = context.FileSystem.EvaluateLocalVersionString(out var branchName);
            var branchInfo = branchName is null ? string.Empty : $" branch: '{branchName}'";

            LogInstanceVersion(context.Logger, context.InstanceOptions.Type, version, branchInfo);
            return Task.CompletedTask;
        });

    public static SuitePreparationPipeline UseDeviceImageCleanup(
        this SuitePreparationPipeline pipeline)
        => pipeline.Use((context, ct) =>
        {
            DeleteDeviceImageFile(context.FileSystem, context.InstanceOptions, context.Logger);
            return Task.CompletedTask;
        });

    public static SuitePreparationPipeline UseResetIfRequested(
        this SuitePreparationPipeline pipeline)
        => pipeline.Use((context, ct) => ResetDependingOnFileFlag(context.FileSystem, context.InstanceOptions, context.RepositoryStore, context.Logger, ct));

    public static SuitePreparationPipeline UseRestoreIfRequested(
        this SuitePreparationPipeline pipeline)
        => pipeline.Use((context, ct) => RestoreDependingOnFileFlag(context.FileSystem, context.InstanceOptions, context.Logger, ct));

    public static SuitePreparationPipeline UseVersionDowngradeDetection(
        this SuitePreparationPipeline pipeline)
        => pipeline.Use(async (context, ct) =>
        {
            var info = await context.FileSystem.DetectVersionDowngrade(context.InstanceOptions, context.Logger, ct);
            return info is null
                ? PreparationResult.Success
                : new VersionDowngradePreparationResult(info);
        });

    public static SuitePreparationPipeline UseRecoveryMode(
        this SuitePreparationPipeline pipeline, WebApplicationBuilder builder)
        => pipeline.Use(async (context, ct) =>
        {
            var decision = await context.FileSystem.UseRecoveryMode(context.InstanceOptions, context.Logger, ct);

            switch (decision)
            {
                case RecoveryDecision.ApplyRecovery:
                    {
                        var modulesFilePath = context.FileSystem.GetModuleVersionsFilePath(context.InstanceOptions);
                        var backupFilePath = context.FileSystem.GetModuleVersionsBackupFilePath(context.InstanceOptions);

                        context.FileSystem.File.Copy(modulesFilePath, backupFilePath);
                        await context.FileSystem.CopyInitialModuleManifestTo(modulesFilePath, ct);

                        LocalInstanceInformationProvider.RunningInRecoveryMode = true;
                        LogRecoveryModeActive(context.Logger, backupFilePath);
                        break;
                    }

                case RecoveryDecision.RecoveryExhausted:
                    return new RecoveryExhaustedPreparationResult();

                default:
                    {
                        var loaderOptions = builder.Configuration.GetModuleLoaderOptions();
                        await context.FileSystem.EnsureModuleVersionsFile(context.InstanceOptions, loaderOptions.ManifestSeedPath, context.Logger, ct);
                        break;
                    }
            }

            return PreparationResult.Success;
        });

    public static SuitePreparationPipeline UseOptionsValidation(
        this SuitePreparationPipeline pipeline, WebApplicationBuilder builder)
        => pipeline.Use((context, _) =>
        {
            // validate appsettings, env vars etc.
            builder.Services.ConfigureAndValidateOptions(context.InstanceOptions);
            return Task.CompletedTask;
        });

    public static SuitePreparationPipeline UseModulePreparation(
        this SuitePreparationPipeline pipeline, WebApplicationBuilder builder)
        => pipeline.Use(async (context, ct) =>
        {
            context.ModuleContext = new ModulePreparationContext(builder,
                context.FileSystem,
                context.InstanceOptions,
                context.RepositoryStore,
                context.LoggerFactory);

            using var modulePipeline = new ModulePreparationPipeline(context.ModuleContext)
                .UseApplyEnqueuedOperations()
                .UseRepositoryOptions()
                .UseModuleLoaderOptions()
                .UseModuleSynchronization()
                .UseModuleHost();

            context.ModuleContext.Result = await modulePipeline.RunAsync(ct);

            // propagate a module-host abort so the outer pipeline / TryRunCoreOs can fall back gracefully
            return context.ModuleContext.Result;
        });

    /// <summary>
    /// If flashing the device failed we may have left a .swu file that locks about 280MB disk space.
    /// HostManagement moves the file away before flash happens, so after startup this file should be gone!
    /// </summary>
    private static void DeleteDeviceImageFile(IFileSystem fileSystem, InstanceOptions instanceOptions, ILogger logger)
    {
        var cacheRoot = fileSystem.GetRootedCacheDirectory(instanceOptions);
        var deviceImageFilePath = fileSystem.Path.Combine(cacheRoot, Shared.Constants.SystemModuleId, Shared.Constants.DeviceImageFileName);

        if (!fileSystem.File.Exists(deviceImageFilePath))
            return;

        try
        {
            fileSystem.File.Delete(deviceImageFilePath);
            LogDeletedDeviceImageFile(logger);
        }
        catch (UnauthorizedAccessException ue)
        {
            LogDeleteDeviceImageFileUnauthorized(logger, ue);
        }
        catch (Exception e)
        {
            LogDeleteDeviceImageFileFailed(logger, e);
        }
    }

    /// <summary>
    /// If file .reset-suite exists in home directory at startup we clean up:
    /// - Home|Cache: remove all subdirectories and their contents. Keep instance file
    /// - Backup: remove all files
    /// </summary>
    private static async Task ResetDependingOnFileFlag(IFileSystem fileSystem, InstanceOptions options, IArtifactRepositoryStore repositoryStore, ILogger logger, CancellationToken cancellationToken)
    {
        if (!fileSystem.ResetFileExists(options))
            return;

        try
        {
            // we'll have to reset our home, caches and backups
            fileSystem.DeleteCacheDirectories(options, logger);

            // actually user has no way to only restore some modules - we'll remove everything
            fileSystem.DeleteHomeDirectories(options, logger);

            fileSystem.ClearBackupFiles(options, logger);

            LogResetModuleManifest(logger);
            var moduleManifestPath = fileSystem.GetModuleVersionsFilePath(options);
            fileSystem.File.Delete(moduleManifestPath);

            LogResetDataVersionInfo(logger);
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);
            fileSystem.File.Delete(dataVersionPath);

            await repositoryStore.Clear(cancellationToken);
        }
        catch (Exception e)
        {
            LogResetWorkspaceFailed(logger, e);
        }
        finally
        {
            fileSystem.DeleteResetFile(options);
        }
    }

    /// <summary>
    /// If a <see cref="RestoreTask"/> is available in home directory restoring the state of backup
    /// defined within the task gets triggered. Module home/cache workspaces are cleared out and
    /// replaced by the contents of the backup.
    /// </summary>
    private static async Task RestoreDependingOnFileFlag(IFileSystem fileSystem, InstanceOptions options, ILogger logger, CancellationToken cancellationToken = default)
    {
        string? restoreFile = null;
        try
        {
            // check if we have the task set
            var restoreTask = await fileSystem.ReadRestoreTask(options, cancellationToken);
            if (restoreTask is null)
                return;// nothing to be done

            restoreFile = fileSystem.Path.GetFileName(restoreTask.BackupPath);

            if (!fileSystem.File.Exists(restoreTask.BackupPath))
            {
                LogRestoreBackupNotFound(logger, restoreTask.BackupPath);
                return;
            }

            LogRestoringBackup(logger, restoreTask.BackupPath);

            // metadata - do some insanity checks (again?!)
            await using var archiveStream = fileSystem.FileStream.New(restoreTask.BackupPath, FileMode.Open, FileAccess.Read);
            var metadata = await BackupReader.GetBackupMetadata(archiveStream, cancellationToken);

            LogBackupMetadata(logger, metadata.SuiteVersion, metadata.SdkVersion, metadata.Modules.Count);

            // on importing a backup various things might happen like in ClusterManagement `packages.json` was
            // changed but in cache we have still other FB versions so nothing will fit together :(
            // best possible way is to also clear the caches.
            fileSystem.DeleteCacheDirectories(options, logger);

            // actually user has no way to only restore some modules we'll remove everything
            fileSystem.DeleteHomeDirectories(options, logger);

            // backup is already validated - this will be recreated later on 
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);
            fileSystem.File.Delete(dataVersionPath);

            // Suite home directory just contains `InstanceId.info`, `modules.json` now
            // now we restore all modules from backup archive to home directory
            var homeDirectory = fileSystem.GetRootedHomeDirectory(options);
            LogRestoringHomeWorkspaces(logger, homeDirectory);
            await BackupReader.ExtractSystemModuleTo(archiveStream, homeDirectory, cancellationToken);
            await BackupReader.ExtractModulesTo(archiveStream, homeDirectory, null, cancellationToken);

            InstanceStartupState.InvalidateLoginsAfterMigration = true;
        }
        catch (Exception ex)
        {
            LogRestoreBackupFailed(logger, ex, restoreFile ?? "unknown");
            throw;
        }
        finally
        {
            // remove restore flag so we won't try multiple times on failure
            fileSystem.DeleteRestoreTask(options);
        }
    }

    [LoggerMessage(LogLevel.Information, "Preparing '{Type}' application version '{Version}':{BranchInfo}")]
    private static partial void LogInstanceVersion(ILogger logger, InstanceType type, string? version, string? branchInfo);

    [LoggerMessage(LogLevel.Warning, "Recovery mode - all modules disabled. Previous configuration stored within '{Path}'")]
    private static partial void LogRecoveryModeActive(ILogger logger, string path);

    [LoggerMessage(LogLevel.Information, "Delete leftover device image file.")]
    private static partial void LogDeletedDeviceImageFile(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Insufficient permissions to delete leftover device image file")]
    private static partial void LogDeleteDeviceImageFileUnauthorized(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to delete leftover device image file.")]
    private static partial void LogDeleteDeviceImageFileFailed(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Reset module manifest")]
    private static partial void LogResetModuleManifest(ILogger logger);

    [LoggerMessage(LogLevel.Debug, "Reset data version info")]
    private static partial void LogResetDataVersionInfo(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to reset workspaces")]
    private static partial void LogResetWorkspaceFailed(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to restore backup file. Backup could not be found in '{Path}'")]
    private static partial void LogRestoreBackupNotFound(ILogger logger, string path);

    [LoggerMessage(LogLevel.Debug, "Restoring backup from '{Backup}'")]
    private static partial void LogRestoringBackup(ILogger logger, string backup);

    [LoggerMessage(LogLevel.Debug, "Backup made with Suite v{SuiteVersion} and Sdk v{SdkVersion} containing {ModuleCount} modules")]
    private static partial void LogBackupMetadata(ILogger logger, string? suiteVersion, string? sdkVersion, int moduleCount);

    [LoggerMessage(LogLevel.Debug, "Restoring home workspaces in '{Home}'")]
    private static partial void LogRestoringHomeWorkspaces(ILogger logger, string home);

    [LoggerMessage(LogLevel.Error, "Failed to restore suite using {Backup}")]
    private static partial void LogRestoreBackupFailed(ILogger logger, Exception exception, string backup);
}
