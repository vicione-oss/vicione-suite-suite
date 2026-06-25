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

    private const string RecoveryFileName = "recovery";
    private const string RestoreFlagFile = ".restore-backup";
    private const string ResetFlagFile = ".reset-suite";

    /// <param name="fileSystem"></param>
    extension(IFileSystem fileSystem)
    {
        /// <summary>
        /// Ensure that the instance id is written to the root folder of the application.
        /// On master instance a constant is used. For others, it's either generated or
        /// can be defined by <see cref="InstanceOptions.IdPreload"/>
        /// </summary>
        /// <param name="instanceOptions"></param>
        internal void EnsureInstanceIdFile(InstanceOptions instanceOptions)
        {
            var localInfoFilePath = fileSystem.GetLocalInstanceIdFilePath(instanceOptions);
            if (fileSystem.File.Exists(localInfoFilePath))
                return;

            var id = instanceOptions.Type == InstanceType.Master
                ? Constants.MasterInstanceGuid // this need to be changed for multi master scenario!
                : (instanceOptions.IdPreload ?? Guid.NewGuid());

            fileSystem.Directory.CreateDirectory(fileSystem.GetRootedHomeDirectory(instanceOptions));
            fileSystem.File.WriteAllText(localInfoFilePath, id.ToString());
        }

        internal async Task<RecoveryDecision> UseRecoveryMode(InstanceOptions instanceOptions, Serilog.ILogger logger, CancellationToken cancellationToken = default)
        {
            if (instanceOptions.Recovery is null || instanceOptions.Recovery.TimespanMinutes <= 0)
            {
                logger.Information("Recovery mode is disabled by configuration");
                return RecoveryDecision.Continue;
            }

            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(instanceOptions);
            if (!fileSystem.File.Exists(recoveryFilePath))
            {
                // file did not exist so we start the counter...
                await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken: cancellationToken);
                return RecoveryDecision.Continue;
            }

            var existingState = await fileSystem.ReadRecoveryState(recoveryFilePath, logger, cancellationToken);
            if (existingState is null)
            {
                logger.Warning("Failed to restore recovery state");
                return RecoveryDecision.Continue;
            }

            var timeElapsed = DateTimeOffset.UtcNow.Subtract(existingState.LastStartup);
            var checkTimespan = TimeSpan.FromMinutes(instanceOptions.Recovery.TimespanMinutes);

            // the last startup is long time ago...reset
            if (timeElapsed > checkTimespan)
            {
                await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken: cancellationToken);
                return RecoveryDecision.Continue;
            }

            // we fail but we can try again once again...
            if (existingState.Startups <= instanceOptions.Recovery.MaxStartupAttempts)
            {
                existingState.Startups++;
                await fileSystem.WriteRecoveryState(recoveryFilePath, existingState, cancellationToken);
                logger.Information("Startup counter increased to {Count}", existingState.Startups);
                return RecoveryDecision.Continue;
            }

            // Recovery was already applied but the suite still crashes — escalate to terminal state
            if (existingState.RecoveryApplied)
            {
                logger.Fatal(
                    "Recovery mode was already applied but the suite crashed {Startups} more times within {Minutes} minutes. Entering terminal failed state",
                    existingState.Startups,
                    instanceOptions.Recovery.TimespanMinutes);
                return RecoveryDecision.RecoveryExhausted;
            }

            // First time hitting threshold — apply recovery and mark it
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, recoveryApplied: true, cancellationToken: cancellationToken);
            logger.Warning("Fallback to recovery mode after {Startups} startups", existingState.Startups);
            return RecoveryDecision.ApplyRecovery;
        }

        internal string GetRootedHomeDirectory(InstanceOptions instanceOptions)
            => string.IsNullOrEmpty(instanceOptions.HomeDirectory)
                ? throw new ConfigurationException(nameof(InstanceOptions.HomeDirectory))
                : fileSystem.GetRootedPath(instanceOptions.HomeDirectory);

        internal string GetRootedCacheDirectory(InstanceOptions instanceOptions)
            => string.IsNullOrEmpty(instanceOptions.CacheDirectory)
                ? throw new ConfigurationException(nameof(InstanceOptions.CacheDirectory))
                : fileSystem.GetRootedPath(instanceOptions.CacheDirectory);

        internal string GetRootedBackupDirectory(InstanceOptions instanceOptions)
            => string.IsNullOrEmpty(instanceOptions.BackupDirectory)
                ? throw new ConfigurationException(nameof(InstanceOptions.BackupDirectory))
                : fileSystem.GetRootedPath(instanceOptions.BackupDirectory);

        internal string GetLocalInstanceIdFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), InstanceIdFileName);

        internal string GetLocalRecoveryFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), RecoveryFileName);

        internal async Task<RecoveryState?> ReadRecoveryState(string recoveryFilePath, Serilog.ILogger logger, CancellationToken cancellationToken = default)
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

        internal Task WriteRecoveryStateReset(string recoveryFilePath, bool recoveryApplied = false, CancellationToken cancellationToken = default)
            => fileSystem.WriteRecoveryState(recoveryFilePath, new RecoveryState { LastStartup = DateTimeOffset.UtcNow, Startups = 1, RecoveryApplied = recoveryApplied }, cancellationToken);

        private async Task WriteRecoveryState(string recoveryFilePath, RecoveryState state, CancellationToken cancellationToken = default)
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

        /// <summary>
        /// Writes a reset file to indicate that the instance should reset its modules on next startup if the file does not already exist.
        /// File name is <see cref="ResetFlagFile"/> within the instance suite home directory.
        /// </summary>    
        public void WriteResetFile(InstanceOptions options)
        {
            if (fileSystem.ResetFileExists(options))
                return;

            fileSystem.File.Create(fileSystem.GetResetFlagPath(options), 0, FileOptions.None).Dispose();
        }

        public bool ResetFileExists(InstanceOptions options)
            => fileSystem.File.Exists(fileSystem.GetResetFlagPath(options));

        public void DeleteResetFile(InstanceOptions options)
            => fileSystem.File.Delete(fileSystem.GetResetFlagPath(options));

        public async Task WriteRestoreTask(InstanceOptions options, RestoreTask restoreTask, CancellationToken cancellationToken = default)
        {
            var restoreTaskPath = fileSystem.GetRestoreTaskPath(options);
            await using var restoreStream = fileSystem.FileStream.New(restoreTaskPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(restoreStream, restoreTask, DefaultJsonSerializerSettings.Default, cancellationToken);
        }

        public async Task<RestoreTask?> ReadRestoreTask(InstanceOptions options, CancellationToken cancellationToken = default)
        {
            var restoreTaskPath = fileSystem.GetRestoreTaskPath(options);
            if (!fileSystem.File.Exists(restoreTaskPath))
                return null;

            await using var restoreStream = fileSystem.FileStream.New(restoreTaskPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<RestoreTask?>(restoreStream, DefaultJsonSerializerSettings.Default, cancellationToken);
        }

        public void DeleteRestoreTask(InstanceOptions options)
        {
            var restoreTaskPath = fileSystem.GetRestoreTaskPath(options);
            fileSystem.File.Delete(restoreTaskPath);
        }

        private string GetRestoreTaskPath(InstanceOptions options)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(options), RestoreFlagFile);

        private string GetResetFlagPath(InstanceOptions options)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(options), ResetFlagFile);

        /// <summary>
        /// Deletes each directory recursively contained in <see cref="InstanceOptions.CacheDirectory"/> 
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public void DeleteCacheDirectories(InstanceOptions options, Serilog.ILogger? logger = null)
            => fileSystem.DeleteChildDirectories(fileSystem.GetRootedCacheDirectory(options), "cache", logger);

        /// <summary>
        /// Deletes each directory recursively contained in <see cref="InstanceOptions.HomeDirectory"/> 
        /// </summary>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public void DeleteHomeDirectories(InstanceOptions options, Serilog.ILogger? logger = null)
            => fileSystem.DeleteChildDirectories(fileSystem.GetRootedHomeDirectory(options), "home", logger);

        private void DeleteChildDirectories(string parentPath, string source, Serilog.ILogger? logger)
        {
            if (!fileSystem.Directory.Exists(parentPath))
                return;

            var directories = fileSystem.Directory.GetDirectories(parentPath);
            fileSystem.TryDeleteDirectories(directories, source, logger);
        }

        public void ClearBackupFiles(InstanceOptions options, Serilog.ILogger? logger = null)
        {
            var backupDirectory = fileSystem.GetRootedBackupDirectory(options);
            if (!fileSystem.Directory.Exists(backupDirectory))
                return;

            var files = fileSystem.Directory.GetFiles(backupDirectory);
            fileSystem.TryDeleteFiles(files, "backup", logger);
        }
    }
}
