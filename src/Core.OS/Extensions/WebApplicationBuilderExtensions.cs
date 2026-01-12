using System.IO.Abstractions;
using Core.OS.Hosting.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Modules.Extensions;

namespace Core.OS.Extensions;

internal static class WebApplicationBuilderExtensions
{
    public static async Task<bool> PrepareSuite(this WebApplicationBuilder builder, IFileSystem fileSystem, InstanceOptions instanceOptions, Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        fileSystem.EnsureInstanceIdFile(instanceOptions);

        // reset clears home, cache and backup folders if file flag is set
        HandleReset(fileSystem, instanceOptions, logger);

        // import a backup if the task is set
        await HandleSuiteRestore(fileSystem, instanceOptions, logger, cancellationToken);

        if (await builder.DetectVersionDowngrade(fileSystem, instanceOptions, logger, cancellationToken))
        {
            // if downgrade host ends suite startup also ends here    
            return false;
        }

        var loaderOptions = builder.Configuration.GetModuleLoaderOptions();
        await fileSystem.PrepareModuleVersionsFile(instanceOptions, loaderOptions.ManifestSeedPath, logger, cancellationToken);

        return true;
    }

    /// <summary>
    /// If file .reset-flag exists in home directory at startup we clean up:
    /// - Home|Cache: remove all subdirectories and their contents. Keep instance file
    /// - Backup: remove all files
    /// </summary>
    private static void HandleReset(IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger)
    {
        try
        {
            if (!fileSystem.ResetFileExists(options))
                return;

            // we'll have to reset our home, caches and backups
            logger.Debug("Clearing workspace cache");
            fileSystem.DeleteCacheDirectories(options, logger);

            // actually use has no way to only restore some modules we'll remove everything
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
    /// If a <see cref="RestoreTask"/> available in home directory restoring the state of backup
    /// defined within the task gets triggered. Module home/cache workspaces are cleared out and
    /// replaced by the contents of the backup.
    /// </summary>
    private static async Task HandleSuiteRestore(IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger, CancellationToken cancellationToken = default)
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

            // actually use has no way to only restore some modules we'll remove everything
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
