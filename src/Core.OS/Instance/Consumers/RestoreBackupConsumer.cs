using System.IO.Abstractions;
using Core.Module.Comparer;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.MessageBus.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Persistence.Commands;
using Core.Shared.Persistence.Events;
using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Contracts;
using MassTransit;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class RestoreBackupConsumer(
    IPipeClient pipeClient,
    IBackupStore backupStore,
    IFileSystem fileSystem,
    ILocalInstanceInformationProvider instanceInformationProvider,
    IOptions<InstanceOptions> options,
    IEnumerable<IModuleHostRequestHandler> moduleHandlers,
    ILogger<RestoreBackupConsumer> logger) : IConsumer<RestoreBackup>
{
    public async Task Consume(ConsumeContext<RestoreBackup> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
            if (instanceInformationProvider.Local.Type != Sdk.Instance.InstanceType.Standalone)
                throw new InvalidOperationException($"Restore is only supported on {Sdk.Instance.InstanceType.Standalone}");

            // Neither configuration was requested.
            if (context.Message is { SystemConfiguration: false, SuiteConfiguration: false })
                return;

            LogPreparingSuiteBackupRestore(logger, correlationId, context.Message.SystemConfiguration, context.Message.SuiteConfiguration);

            // TODO: user should select a backup from a list of available ones in UI
            // TODO: should we make a backup before we set the restore flags?
            var systemConfiguration = await ReadBackupArchive(context);
            var currentSystemConfiguration = systemConfiguration is null ? null : await GetCurrentSystemConfiguration(context.CancellationToken);
            var networkChanges = HasNetworkChanges(currentSystemConfiguration, systemConfiguration);

            // HostManagement would only reject after the modules have restored their data.
            var capabilityError = await GetDisabledCapabilityError(context.Message, currentSystemConfiguration, systemConfiguration, networkChanges, context.CancellationToken);
            if (capabilityError is not null)
            {
                LogRestoreRefused(logger, correlationId, capabilityError.ErrorCode, capabilityError.Message);

                await context.Publish(new RestoreBackupPrepared(context.Message.SuiteConfiguration, capabilityError));
                return;
            }

            // Stored only after every check, so that a refused restore leaves no copy of the backup on the device.
            if (context.Message.SuiteConfiguration)
                await PrepareSuiteRestoreOnRestart(context);

            await CallModuleRestore(context);

            // Published before applying the system configuration, because that can restart the
            // system immediately.
            LogPublishBackupPreparedEvent(logger, correlationId);

            await context.Publish(new RestoreBackupPrepared(context.Message.SuiteConfiguration)).ConfigureAwait(false);

            // Applying the system configuration makes HostManagement trigger the restart.
            if (await ApplySystemConfiguration(context, systemConfiguration, networkChanges))
                return;

            // A suite restore needs a software restart.
            if (context.Message.SuiteConfiguration)
            {
                var controlCommand = new ControlInstance
                {
                    InstanceId = instanceInformationProvider.Local.Id,
                    Action = InstanceCommand.Restart,
                    Delay = TimeSpan.FromSeconds(5),
                };

                await context.SendToInstance(controlCommand, instanceInformationProvider.Local.Id, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId);

            var errorCode = ex is UnsupportedBackupFormatException ? RestoreBackupPrepared.BackupFormatNotSupported : 100;
            await context.Publish(new RestoreBackupPrepared(context.Message.SuiteConfiguration, new ErrorInfo(errorCode, ex.Message)));

            // After a failure the suite is not restored from the backup.
            fileSystem.DeleteRestoreTask(options.Value);
        }
        finally
        {
            // The upload has been copied to the backup store where needed, so it is no longer required.
            DeleteUploadedBackupFile(context.Message.BackupFilePath, correlationId);
        }
    }

    private void DeleteUploadedBackupFile(string backupFilePath, Guid correlationId)
    {
        try
        {
            if (fileSystem.File.Exists(backupFilePath))
                fileSystem.File.Delete(backupFilePath);
        }
        catch (Exception ex)
        {
            LogDeleteUploadedBackupFileFailed(logger, ex, correlationId, backupFilePath);
        }
    }

    private async Task CallModuleRestore(ConsumeContext<RestoreBackup> context)
    {
        // Every IModuleHostRequestHandlers implementation gets the call.
        foreach (var moduleHandler in moduleHandlers)
        {
            try
            {
                await moduleHandler.OnRestore(context.CancellationToken);
            }
            catch (Exception e)
            {
                LogCallRestoreHandlerFailedOn(logger, e, context.Message.CorrelationId, moduleHandler.GetType().Name);
            }
        }
    }

    private async Task<SystemConfiguration?> ReadBackupArchive(ConsumeContext<RestoreBackup> context)
    {
        await using Stream contentStream = fileSystem.File.OpenRead(context.Message.BackupFilePath);

        // Throws when the metadata cannot be extracted, which means the archive is invalid.
        var comparer = new StringVersionComparer();
        var local = instanceInformationProvider.Local;
        var metadata = await BackupReader.GetBackupMetadata(contentStream, context.CancellationToken);

        if (instanceInformationProvider.Local.Id != metadata.InstanceId)
            throw new InvalidOperationException($"Restore backup of another instances is not supported.");

        if (comparer.Compare(local.SdkVersion, metadata.SdkVersion) < 0)
            throw new InvalidOperationException($"Restore backup created with newer sdk version {metadata.SdkVersion} is not supported");

        if (comparer.Compare(local.Version, metadata.SuiteVersion) < 0)
            throw new InvalidOperationException($"Restore backup created with newer Core.OS version {metadata.SuiteVersion} is not supported");

        if (context.Message.SystemConfiguration)
        {
            // HM configuration should be updated to version of backup
            return await BackupReader.GetSystemConfiguration(contentStream, context.CancellationToken);
        }

        return null;
    }

    private async Task PrepareSuiteRestoreOnRestart(ConsumeContext<RestoreBackup> context)
    {
        await using Stream contentStream = fileSystem.File.OpenRead(context.Message.BackupFilePath);

        var backupFile = await StoreBackupFile(contentStream, context.CancellationToken);
        var backupPath = fileSystem.Path.Combine(fileSystem.GetRootedBackupDirectory(options.Value), backupFile);

        // A flag file in AppData is picked up on the next startup.
        var restoreTask = new RestoreTask(backupPath, context.Message.SuiteConfiguration, context.Message.SystemConfiguration, DateTimeOffset.Now);
        await fileSystem.WriteRestoreTask(options.Value, restoreTask, context.CancellationToken);

        LogPreparedRestoreBackupOnRestart(logger, context.Message.CorrelationId, backupFile);
    }

    private async Task<ErrorInfo?> GetDisabledCapabilityError(RestoreBackup message, SystemConfiguration? current, SystemConfiguration? toBeRestored,
        bool networkChanges, CancellationToken cancellationToken)
    {
        var capabilities = await pipeClient.GetSupportedCapabilitiesOrNull(logger, cancellationToken);
        if (capabilities is null)
            return null;

        // Without network changes HostManagement does not restart, so the Suite restarts itself.
        if (message.SuiteConfiguration && !networkChanges && capabilities.Topics.RestartService is CapabilityStatus.Disabled)
            return new ErrorInfo(RestoreBackupPrepared.RestartServiceDisabled, CapabilityErrors.Disabled(Topics.RestartService));

        if (current is null || toBeRestored is null)
            return null;

        var disabledSettings = DisabledSettings.ChangedBy(current, toBeRestored, capabilities.Settings);
        if (disabledSettings.Count > 0)
            return new ErrorInfo(RestoreBackupPrepared.SettingsDisabled, string.Join(", ", disabledSettings));

        return null;
    }

    private async Task<bool> ApplySystemConfiguration(ConsumeContext<RestoreBackup> context, SystemConfiguration? systemConfiguration, bool networkChanges)
    {
        // Applying SystemConfiguration needs a machine restart.
        if (!context.Message.SystemConfiguration || systemConfiguration is null)
            return false;

        LogApplySystemConfigurationFromBackup(logger, context.Message.CorrelationId, systemConfiguration.Version, networkChanges);

        // Applying configuration from a backup with network changes triggers a system or suite
        // restart.
        var result = await pipeClient.SetSystemConfiguration(systemConfiguration, context.CancellationToken);
        if (result?.Status != OperationStatus.Success)
            throw new InvalidOperationException(result?.Message);

        return networkChanges;
    }

    private async Task<SystemConfiguration?> GetCurrentSystemConfiguration(CancellationToken cancellationToken)
    {
        var configurationResult = await pipeClient.GetSystemConfiguration(cancellationToken);

        return configurationResult?.Status == OperationStatus.Success ? configurationResult.Configuration : null;
    }

    private static bool HasNetworkChanges(SystemConfiguration? current, SystemConfiguration? toBeRestored)
    {
        if (current is null || toBeRestored is null)
            return false;

        // Configuration touching the linux netplan can restart the network interface, which in turn
        // restarts the suite.
        if (current.NetworkDNSSettings.Equals(toBeRestored.NetworkDNSSettings) &&
            current.NetworkInterfaces.SequenceEqual(toBeRestored.NetworkInterfaces) &&
            current.NetworkNTPSettings.Equals(toBeRestored.NetworkNTPSettings) &&
            current.NetworkProxySettings.Equals(toBeRestored.NetworkProxySettings))
            return false;

        return true;
    }

    private async Task<string> StoreBackupFile(Stream contentStream, CancellationToken cancellationToken)
    {
        if (contentStream.CanSeek)
            contentStream.Seek(0, SeekOrigin.Begin);

        await using var fileStream = backupStore.CreateBackupFile(out var filename);
        await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

        return filename;
    }


    [LoggerMessage(Level = LogLevel.Information, Message = "Preparing suite backup configuration (system='{RestoreSystem}', suite='{RestoreSuite}') correlated by {CorrelationId}")]
    private static partial void LogPreparingSuiteBackupRestore(ILogger logger, Guid correlationId, bool restoreSystem, bool restoreSuite);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Publish backup prepared event correlated by {CorrelationId}")]
    private static partial void LogPublishBackupPreparedEvent(ILogger logger, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Call restore handler failed on {Handler} correlated by {CorrelationId}")]
    private static partial void LogCallRestoreHandlerFailedOn(ILogger logger, Exception exception, Guid correlationId, string handler);

    [LoggerMessage(Level = LogLevel.Information, Message = "Prepared restore backup {FileName} on restart correlated by {CorrelationId}")]
    private static partial void LogPreparedRestoreBackupOnRestart(ILogger logger, Guid correlationId, string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused backup restore, because HostManagement disables a needed capability ({ErrorCode}: {Message}) correlated by {CorrelationId}")]
    private static partial void LogRestoreRefused(ILogger logger, Guid correlationId, int errorCode, string? message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Apply system configuration version='{Version}' from backup archive (network change:{Change}) correlated by {CorrelationId}")]
    private static partial void LogApplySystemConfigurationFromBackup(ILogger logger, Guid correlationId, int version, bool change);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process backup restore correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete uploaded backup file '{BackupFilePath}' correlated by {CorrelationId}")]
    private static partial void LogDeleteUploadedBackupFileFailed(ILogger logger, Exception exception, Guid correlationId, string backupFilePath);
}
