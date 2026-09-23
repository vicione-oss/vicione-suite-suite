using System.IO.Abstractions;
using System.Text.Json;
using Core.Module.Extensions;
using Core.OS.Extensions;
using Core.OS.Instance.Contracts;
using Core.Shared;
using Sdk.Instance;
using Sdk.Messaging;

namespace Core.OS.Instance.Extensions;

internal static partial class IFileSystemExtensions
{
    public const string InstanceIdFileName = "InstanceId.info";

    private const string RecoveryFileName = "recovery";
    private const string RestoreFlagFile = ".restore-backup";
    private const string ResetFlagFile = ".reset-suite";

    extension(IFileSystem fileSystem)
    {
        /// <summary>
        /// Ensure that the instance id is written to the root folder of the application.
        /// On master instance a constant is used. For others, it's either generated or
        /// can be defined by <see cref="InstanceOptions.IdPreload"/>
        /// </summary>
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

        internal async Task<RecoveryDecision> UseRecoveryMode(InstanceOptions instanceOptions, ILogger logger, CancellationToken cancellationToken = default)
        {
            if (instanceOptions.Recovery is null || instanceOptions.Recovery.TimespanMinutes <= 0)
            {
                LogRecoveryModeDisabled(logger);
                return RecoveryDecision.Continue;
            }

            var recoveryFilePath = fileSystem.GetLocalRecoveryFilePath(instanceOptions);
            if (!fileSystem.File.Exists(recoveryFilePath))
            {
                // The file was missing, so the counter starts here.
                await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken: cancellationToken);
                return RecoveryDecision.Continue;
            }

            var existingState = await fileSystem.ReadRecoveryState(recoveryFilePath, logger, cancellationToken);
            if (existingState is null)
            {
                LogRecoveryStateReadFailed(logger);
                return RecoveryDecision.Continue;
            }

            var timeElapsed = DateTimeOffset.UtcNow.Subtract(existingState.LastStartup);
            var checkTimespan = TimeSpan.FromMinutes(instanceOptions.Recovery.TimespanMinutes);

            // The last startup is long past, so the counter resets.
            if (timeElapsed > checkTimespan)
            {
                await fileSystem.WriteRecoveryStateReset(recoveryFilePath, cancellationToken: cancellationToken);
                return RecoveryDecision.Continue;
            }

            // This attempt fails, but another one follows.
            if (existingState.Startups <= instanceOptions.Recovery.MaxStartupAttempts)
            {
                existingState.Startups++;
                await fileSystem.WriteRecoveryState(recoveryFilePath, existingState, cancellationToken);
                LogStartupCounterIncreased(logger, existingState.Startups);
                return RecoveryDecision.Continue;
            }

            // Recovery was already applied but the suite still crashes — escalate to terminal state
            if (existingState.RecoveryApplied)
            {
                LogRecoveryExhausted(logger, existingState.Startups, instanceOptions.Recovery.TimespanMinutes);
                return RecoveryDecision.RecoveryExhausted;
            }

            // First time hitting threshold — apply recovery and mark it
            await fileSystem.WriteRecoveryStateReset(recoveryFilePath, recoveryApplied: true, cancellationToken: cancellationToken);
            LogFallingBackToRecovery(logger, existingState.Startups);
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

        /// <summary>
        /// Returns the persisted instance id, or null when the instance id file has
        /// not been created yet (e.g. before the preparation pipeline writes it on
        /// the first boot).
        /// </summary>
        internal string? ReadLocalInstanceId(InstanceOptions instanceOptions)
        {
            var localInfoFilePath = fileSystem.GetLocalInstanceIdFilePath(instanceOptions);
            return fileSystem.File.Exists(localInfoFilePath)
                ? fileSystem.File.ReadAllText(localInfoFilePath).Trim()
                : null;
        }

        internal string GetLocalRecoveryFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), RecoveryFileName);

        internal async Task<RecoveryState?> ReadRecoveryState(string recoveryFilePath, ILogger logger, CancellationToken cancellationToken = default)
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
                    LogReadRecoveryStateContentFailed(logger, e1, content);
                }
                catch (Exception e2)
                {
                    LogReadRecoveryStateFileFailed(logger, e2, recoveryFilePath);
                }
                try
                {
                    LogDeletingRecoveryStateFile(logger);
                    fileSystem.File.Delete(recoveryFilePath);
                }
                catch (Exception e3)
                {
                    LogDeleteRecoveryStateFailed(logger, e3);
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
        public void DeleteCacheDirectories(InstanceOptions options, ILogger? logger = null)
            => fileSystem.DeleteChildDirectories(fileSystem.GetRootedCacheDirectory(options), "cache", logger);

        /// <summary>
        /// Deletes each directory recursively contained in <see cref="InstanceOptions.HomeDirectory"/>
        /// </summary>
        public void DeleteHomeDirectories(InstanceOptions options, ILogger? logger = null)
            => fileSystem.DeleteChildDirectories(fileSystem.GetRootedHomeDirectory(options), "home", logger);

        private void DeleteChildDirectories(string parentPath, string source, ILogger? logger)
        {
            if (!fileSystem.Directory.Exists(parentPath))
                return;

            var directories = fileSystem.Directory.GetDirectories(parentPath);
            fileSystem.TryDeleteDirectories(directories, source, logger);
        }

        public void ClearBackupFiles(InstanceOptions options, ILogger? logger = null)
        {
            var backupDirectory = fileSystem.GetRootedBackupDirectory(options);
            if (!fileSystem.Directory.Exists(backupDirectory))
                return;

            var files = fileSystem.Directory.GetFiles(backupDirectory);
            fileSystem.TryDeleteFiles(files, "backup", logger);
        }
    }
}
