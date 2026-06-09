using System.IO.Abstractions;
using Core.OS.Hosting.Contracts;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Modules.Extensions;
using Microsoft.Extensions.Options;

namespace Core.OS.Hosting.Extensions;

internal static class SuitePreparationPipelineExtensions
{
    public static SuitePreparationPipeline UseInstanceId(
        this SuitePreparationPipeline pipeline, IFileSystem fileSystem, InstanceOptions options)
        => pipeline.Use(() => fileSystem.EnsureInstanceIdFile(options));

    public static SuitePreparationPipeline UseDeviceImageCleanup(
        this SuitePreparationPipeline pipeline, IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger)
        => pipeline.Use(() => DeleteDeviceImageFile(fileSystem, options, logger));

    public static SuitePreparationPipeline UseResetFile(
        this SuitePreparationPipeline pipeline, IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger)
        => pipeline.Use(ct => ResetDependingOnFileFlag(fileSystem, options, logger, ct));

    public static SuitePreparationPipeline UseRestore(
        this SuitePreparationPipeline pipeline, IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger)
        => pipeline.Use(ct => RestoreDependingOnFileFlag(fileSystem, options, logger, ct));

    public static SuitePreparationPipeline UseVersionDowngradeCheck(
        this SuitePreparationPipeline pipeline, IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger)
        => pipeline.Use(async ct =>
        {
            var info = await fileSystem.DetectVersionDowngrade(options, logger, ct);
            return info is null
                ? PreparationResult.Success
                : new VersionDowngradePreparationResult(info);
        });

    public static SuitePreparationPipeline UseRecoveryMode(
        this SuitePreparationPipeline pipeline, WebApplicationBuilder builder, IFileSystem fileSystem,
        InstanceOptions options, Serilog.ILogger logger)
        => pipeline.Use(async ct =>
        {
            if (await fileSystem.UseRecoveryMode(options, logger, ct))
            {
                var modulesFilePath = fileSystem.GetModuleVersionsFilePath(options);
                var backupFilePath = fileSystem.GetModuleVersionsBackupFilePath(options);

                fileSystem.File.Copy(modulesFilePath, backupFilePath);
                await fileSystem.CopyInitialModuleManifestTo(modulesFilePath, ct);

                LocalInstanceInformationProvider.RunningInRecoveryMode = true;
                logger.Warning("Recovery mode - all modules disabled. Previous configuration stored within '{Path}'", backupFilePath);
            }
            else
            {
                var loaderOptions = builder.Configuration.GetModuleLoaderOptions();
                await fileSystem.EnsureModuleVersionsFile(options, loaderOptions.ManifestSeedPath, logger, ct);
            }

            return PreparationResult.Success;
        });

    /// <summary>
    /// If flashing the device failed we may have left a .swu file that locks about 280MB disk space.
    /// HostManagement moves the file away before flash happens, so after startup this file should be gone!
    /// </summary>
    private static void DeleteDeviceImageFile(IFileSystem fileSystem, InstanceOptions instanceOptions, Serilog.ILogger logger)
    {
        var cacheRoot = fileSystem.GetRootedCacheDirectory(instanceOptions);
        var deviceImageFilePath = fileSystem.Path.Combine(cacheRoot, Shared.Constants.SystemModuleId, Shared.Constants.DeviceImageFileName);

        if (!fileSystem.File.Exists(deviceImageFilePath))
            return;

        try
        {
            fileSystem.File.Delete(deviceImageFilePath);
            logger.Information("Delete leftover device image file.");
        }
        catch (UnauthorizedAccessException ue)
        {
            logger.Error(ue, "Insufficient permissions to delete leftover device image file");
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to delete leftover device image file.");
        }
    }

    /// <summary>
    /// If file .reset-suite exists in home directory at startup we clean up:
    /// - Home|Cache: remove all subdirectories and their contents. Keep instance file
    /// - Backup: remove all files
    /// </summary>
    private static async Task ResetDependingOnFileFlag(IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger, CancellationToken cancellationToken)
    {
        if (!fileSystem.ResetFileExists(options))
            return;

        try
        {
            // we'll have to reset our home, caches and backups
            logger.Debug("Clearing workspace cache");
            fileSystem.DeleteCacheDirectories(options, logger);

            // actually user has no way to only restore some modules - we'll remove everything
            logger.Debug("Clearing workspace home");
            fileSystem.DeleteHomeDirectories(options, logger);

            logger.Debug("Clearing backup workspace");
            fileSystem.ClearBackupFiles(options, logger);

            logger.Debug("Reset module manifest");
            var moduleManifestPath = fileSystem.GetModuleVersionsFilePath(options);
            fileSystem.File.Delete(moduleManifestPath);

            logger.Debug("Reset data version info");
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);
            fileSystem.File.Delete(dataVersionPath);

            logger.Debug("Clear artifact sources");
            using var repoStore = new ArtifactRepositoryStore(fileSystem, Options.Create(options));
            await repoStore.Clear(cancellationToken);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to reset workspaces");
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
    private static async Task RestoreDependingOnFileFlag(IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger, CancellationToken cancellationToken = default)
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
                logger.Error("Failed to restore backup file. Backup could not be found in '{Path}'", restoreTask.BackupPath);
                return;
            }

            logger.Debug("Restoring backup from '{Backup}'", restoreTask.BackupPath);

            // metadata - do some insanity checks (again?!)
            await using var archiveStream = fileSystem.FileStream.New(restoreTask.BackupPath, FileMode.Open, FileAccess.Read);
            var metadata = await BackupReader.GetBackupMetadata(archiveStream, cancellationToken);

            logger.Debug("Backup made with Suite v{SuiteVersion} and Sdk v{SdkVersion} containing {ModuleCount} modules",
                metadata.SuiteVersion,
                metadata.SdkVersion,
                metadata.Modules.Count);

            // on importing a backup various things might happen like in ClusterManagement `packages.json` was
            // changed but in cache we have still other FB versions so nothing will fit together :(
            // best possible way is to also clear the caches.
            logger.Debug("Clearing workspace cache");
            fileSystem.DeleteCacheDirectories(options);

            // actually user has no way to only restore some modules we'll remove everything
            logger.Debug("Clearing workspace home");
            fileSystem.DeleteHomeDirectories(options);

            // backup is already validated - this will be recreated later on 
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);
            fileSystem.File.Delete(dataVersionPath);

            // Suite home directory just contains `InstanceId.info`, `modules.json` now
            // now we restore all modules from backup archive to home directory
            var homeDirectory = fileSystem.GetRootedHomeDirectory(options);
            logger.Debug("Restoring home workspaces in '{Home}'", homeDirectory);
            BackupReader.ExtractSystemModuleTo(archiveStream, homeDirectory);
            BackupReader.ExtractModulesTo(archiveStream, homeDirectory);

            InstanceStartupState.InvalidateLoginsAfterMigration = true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to restore suite using {Backup}", restoreFile ?? "unknown");
            throw;
        }
        finally
        {
            // remove restore flag so we won't try multiple times on failure
            fileSystem.DeleteRestoreTask(options);
        }
    }
}
