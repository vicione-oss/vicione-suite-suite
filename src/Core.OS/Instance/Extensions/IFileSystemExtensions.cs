using System.IO.Abstractions;
using System.Text.Json;
using Core.Module.Extensions;
using Core.OS.Extensions;
using Core.OS.Instance.Contracts;
using Core.Shared;
using Sdk.Instance;
using Sdk.Messaging;

namespace Core.OS.Instance.Extensions;

internal static class IFileSystemExtensions
{
    public const string InstanceIdFileName = "InstanceId.info";
    public const string RecoveryFileName = "recovery";

    private const string RestoreFlagFile = ".restore-backup";
    private const string ResetFlagFile = ".reset-suite";


    /// <summary>
    /// Ensure that the instance id is written to the root folder of the application.
    /// On master instance a constant is used. For others, it's either generated or
    /// can be defined by <see cref="InstanceOptions.IdPreload"/>
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="instanceOptions"></param>
    internal static void EnsureInstanceIdFile(this IFileSystem fileSystem, InstanceOptions instanceOptions)
    {
        var localInfoFilePath = fileSystem.GetLocalInstanceIdFilePath(instanceOptions);
        if (fileSystem.File.Exists(localInfoFilePath))
            return;

        var id = instanceOptions.Type == InstanceType.Master
            ? Shared.Constants.MasterInstanceGuid // this need to be changed for multi master scenario!
            : (instanceOptions.IdPreload ?? Guid.NewGuid());

        fileSystem.Directory.CreateDirectory(fileSystem.GetRootedHomeDirectory(instanceOptions));
        fileSystem.File.WriteAllText(localInfoFilePath, id.ToString());
    }

    internal static async Task<bool> UseRecoveryMode(this IFileSystem fileSystem, InstanceOptions instanceOptions, Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        if (instanceOptions.Recovery is null || instanceOptions.Recovery.TimespanMinutes <= 0)
        {
            logger.Information("Recovery mode is disabled by configuration");
            return false;
        }

        var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(instanceOptions);
        if (!fileSystem.File.Exists(recoveryFilePath))
        {
            // file did not exist so we start the counter...
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken);
            return false;
        }

        var existingState = await fileSystem.ReadRecoveryState(recoveryFilePath, logger, cancellationToken);
        if (existingState is null)
        {
            logger.Warning("Failed to restore recovery state");
            return false;
        }

        var timeElapsed = DateTime.UtcNow.Subtract(existingState.LastStartup);
        var checkTimespan = TimeSpan.FromMinutes(instanceOptions.Recovery.TimespanMinutes);

        // the last startup is long time ago...reset
        if (timeElapsed > checkTimespan)
        {
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken);
            return false;
        }

        // we fail but we can try again once again...
        if (existingState.Startups <= instanceOptions.Recovery.MaxStartupAttempts)
        {
            existingState.Startups++;
            await fileSystem.WriteRecoveryState(recoveryFilePath, existingState, cancellationToken);
            logger.Information("Startup counter increased to {Count}", existingState.Startups);
            return false;
        }

        // now it's bad - we failed x times within z minutes! RESET MODULES
        await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken);
        logger.Warning("Fallback to recovery mode after {Startups} startups", existingState.Startups);
        return true;
    }

    internal static string GetRootedHomeDirectory(this IFileSystem fileSystem, InstanceOptions instanceOptions)
        => string.IsNullOrEmpty(instanceOptions.HomeDirectory)
            ? throw new ConfigurationException(nameof(InstanceOptions.HomeDirectory))
            : fileSystem.GetRootedPath(instanceOptions.HomeDirectory);

    internal static string GetRootedCacheDirectory(this IFileSystem fileSystem, InstanceOptions instanceOptions)
        => string.IsNullOrEmpty(instanceOptions.CacheDirectory)
            ? throw new ConfigurationException(nameof(InstanceOptions.CacheDirectory))
            : fileSystem.GetRootedPath(instanceOptions.CacheDirectory);

    internal static string GetRootedBackupDirectory(this IFileSystem fileSystem, InstanceOptions instanceOptions)
        => string.IsNullOrEmpty(instanceOptions.BackupDirectory)
            ? throw new ConfigurationException(nameof(InstanceOptions.BackupDirectory))
            : fileSystem.GetRootedPath(instanceOptions.BackupDirectory);

    internal static string GetLocalInstanceIdFilePath(this IFileSystem fileSystem, InstanceOptions instanceOptions)
        => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), InstanceIdFileName);

    internal static string GetLocalRecoveryFilePath(this IFileSystem fileSystem, InstanceOptions instanceOptions)
        => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), RecoveryFileName);

    internal static async Task<RecoveryState?> ReadRecoveryState(this IFileSystem fileSystem, string recoveryFilePath, Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var fs = fileSystem.FileStream.New(recoveryFilePath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });

            return await JsonSerializer.DeserializeAsync<RecoveryState>(fs, DefaultJsonSerializerSettings.Default, cancellationToken);
        }
        catch (Exception e1)
        {
            try
            {
                var content = await fileSystem.File.ReadAllTextAsync(recoveryFilePath, cancellationToken);
                logger.Error(e1, "Failed to read recovery state. Content: {FileContent}", content);
            }
            catch (Exception e2)
            {
                logger.Error(e2, "Failed to read recovery state. File '{FilePath}' is corrupt", recoveryFilePath);
            }
            try
            {
                logger.Information("Attempting to delete recovery state file");
                fileSystem.File.Delete(recoveryFilePath);
            }
            catch (Exception e3)
            {
                logger.Fatal(e3, "Failed to delete recovery state");
            }
        }
        return null;
    }

    internal static Task WriteRecoveryStateReset(this IFileSystem fileSystem, string recoveryFilePath, CancellationToken cancellationToken = default)
        => fileSystem.WriteRecoveryState(recoveryFilePath, new RecoveryState { LastStartup = DateTime.UtcNow, Startups = 1 }, cancellationToken);

    private static async Task WriteRecoveryState(this IFileSystem fileSystem, string recoveryFilePath, RecoveryState state, CancellationToken cancellationToken = default)
    {
        await using var fs = fileSystem.FileStream.New(recoveryFilePath, new FileStreamOptions
        {
            Mode = FileMode.Create, // ensure we rewrite it!
            Access = FileAccess.Write,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });

        await JsonSerializer.SerializeAsync(fs, state, DefaultJsonSerializerSettings.Default, cancellationToken);
    }

    public static void WriteResetFile(this IFileSystem fileSystem, InstanceOptions options)
        => fileSystem.File.Create(fileSystem.GetResetFlagPath(options), 0, FileOptions.None);

    public static bool ResetFileExists(this IFileSystem fileSystem, InstanceOptions options)
        => fileSystem.File.Exists(fileSystem.GetResetFlagPath(options));

    public static void DeleteResetFile(this IFileSystem fileSystem, InstanceOptions options)
        => fileSystem.File.Delete(fileSystem.GetResetFlagPath(options));

    public static async Task WriteRestoreTask(this IFileSystem fileSystem, InstanceOptions options, RestoreTask restoreTask, CancellationToken cancellationToken = default)
    {
        var restoreTaskPath = fileSystem.GetRestoreTaskPath(options);
        await using var restoreStream = fileSystem.FileStream.New(restoreTaskPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(restoreStream, restoreTask, DefaultJsonSerializerSettings.Default, cancellationToken);
    }

    public static async Task<RestoreTask?> ReadRestoreTask(this IFileSystem fileSystem, InstanceOptions options, CancellationToken cancellationToken = default)
    {
        var restoreTaskPath = fileSystem.GetRestoreTaskPath(options);
        if (!fileSystem.File.Exists(restoreTaskPath))
            return null;

        await using var restoreStream = fileSystem.FileStream.New(restoreTaskPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<RestoreTask?>(restoreStream, DefaultJsonSerializerSettings.Default, cancellationToken);
    }

    public static void DeleteRestoreTask(this IFileSystem fileSystem, InstanceOptions options)
    {
        var restoreTaskPath = fileSystem.GetRestoreTaskPath(options);
        fileSystem.File.Delete(restoreTaskPath);
    }

    private static string GetRestoreTaskPath(this IFileSystem fileSystem, InstanceOptions options)
        => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(options), RestoreFlagFile);

    private static string GetResetFlagPath(this IFileSystem fileSystem, InstanceOptions options)
        => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(options), ResetFlagFile);

    /// <summary>
    /// Deletes each directory recursively contained in <see cref="InstanceOptions.CacheDirectory"/> 
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public static void DeleteCacheDirectories(this IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger? logger = null)
        => fileSystem.DeleteChildDirectories(fileSystem.GetRootedCacheDirectory(options), "cache", logger);

    /// <summary>
    /// Deletes each directory recursively contained in <see cref="InstanceOptions.HomeDirectory"/> 
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public static void DeleteHomeDirectories(this IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger? logger = null)
        => fileSystem.DeleteChildDirectories(fileSystem.GetRootedHomeDirectory(options), "home", logger);

    private static void DeleteChildDirectories(this IFileSystem fileSystem, string parentPath, string source, Serilog.ILogger? logger)
    {
        var directories = fileSystem.Directory.GetDirectories(parentPath);
        fileSystem.TryDeleteDirectories(directories, source, logger);
    }

    public static void ClearBackupFiles(this IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger? logger = null)
    {
        var backupDirectory = fileSystem.GetRootedBackupDirectory(options);
        if (!fileSystem.Directory.Exists(backupDirectory))
            return;

        var files = fileSystem.Directory.GetFiles(backupDirectory);
        fileSystem.TryDeleteFiles(files, "backup", logger);
    }
}
