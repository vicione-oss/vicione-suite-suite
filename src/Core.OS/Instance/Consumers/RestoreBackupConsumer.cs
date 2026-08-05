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

            // nothing to do at all
            if (context.Message is { SystemConfiguration: false, SuiteConfiguration: false })
                return;

            LogPreparingSuiteBackupRestore(logger, correlationId, context.Message.SystemConfiguration, context.Message.SuiteConfiguration);

            // TODO: user should select a backup from a list of available ones in UI
            // TODO: should we make a backup before we set the restore flags?
            var systemConfiguration = await ProcessBackupArchive(context);

            await CallModuleRestore(context);

            // we can't publish the event later because applying the system configuration
            // can lead to immediate restart of the system
            LogPublishBackupPreparedEvent(logger, correlationId);

            await context.Publish(new RestoreBackupPrepared(context.Message.SuiteConfiguration)).ConfigureAwait(false);

            // if we have to apply system configuration restart by HM gets triggered
            if (await ApplySystemConfiguration(context, systemConfiguration))
                return;

            // apply restore on suite will need a software restart
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

            await context.Publish(new RestoreBackupPrepared(context.Message.SuiteConfiguration, new ErrorInfo(100, ex.Message)));

            // something went wrong - don't try to restore suite from backup
            fileSystem.DeleteRestoreTask(options.Value);
        }
        finally
        {
            // the uploaded backup file has been copied to the backup store (if needed) and is no longer required
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
        // get implementations of IModuleHostRequestHandlers and call them
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

    private async Task<SystemConfiguration?> ProcessBackupArchive(ConsumeContext<RestoreBackup> context)
    {
        await using Stream contentStream = fileSystem.File.OpenRead(context.Message.BackupFilePath);

        // this will throw if metadata can't be extracted and it is no valid archive
        var comparer = new StringVersionComparer();
        var local = instanceInformationProvider.Local;
        var metadata = await BackupReader.GetBackupMetadata(contentStream, context.CancellationToken);

        if (instanceInformationProvider.Local.Id != metadata.InstanceId)
            throw new InvalidOperationException($"Restore backup of another instances is not supported.");

        if (comparer.Compare(local.SdkVersion, metadata.SdkVersion) < 0)
            throw new InvalidOperationException($"Restore backup created with newer sdk version {metadata.SdkVersion} is not supported");

        if (comparer.Compare(local.Version, metadata.SuiteVersion) < 0)
            throw new InvalidOperationException($"Restore backup created with newer Core.OS version {metadata.SuiteVersion} is not supported");

        if (context.Message.SuiteConfiguration)
        {
            var backupFile = await StoreBackupFile(contentStream, context.CancellationToken);
            var backupPath = fileSystem.Path.Combine(fileSystem.GetRootedBackupDirectory(options.Value), backupFile);

            // we write a file flag to AppData and react on startup on it
            var restoreTask = new RestoreTask(backupPath, context.Message.SuiteConfiguration, context.Message.SystemConfiguration, DateTimeOffset.Now);
            await fileSystem.WriteRestoreTask(options.Value, restoreTask, context.CancellationToken);

            LogPreparedRestoreBackupOnRestart(logger, context.Message.CorrelationId, backupFile);
        }

        if (context.Message.SystemConfiguration)
        {
            // HM configuration should be updated to version of backup
            return await BackupReader.GetSystemConfiguration(contentStream, context.CancellationToken);
        }

        return null;
    }

    private async Task<bool> ApplySystemConfiguration(ConsumeContext<RestoreBackup> context, SystemConfiguration? systemConfiguration)
    {
        // apply SystemConfiguration will need a machine restart
        if (!context.Message.SystemConfiguration || systemConfiguration is null)
            return false;

        var networkChanges = await HasNetworkChanges(systemConfiguration, context.CancellationToken);

        LogApplySystemConfigurationFromBackup(logger, context.Message.CorrelationId, systemConfiguration.Version, networkChanges);

        // If we apply configuration from backup and we have network changes
        // a restart of the system/suite will be triggered
        var result = await pipeClient.SetSystemConfiguration(systemConfiguration, context.CancellationToken);
        if (result?.Status != OperationStatus.Success)
            throw new InvalidOperationException(result?.Message);

        return networkChanges;
    }

    private async Task<bool> HasNetworkChanges(SystemConfiguration? toBeRestored, CancellationToken cancellationToken)
    {
        if (toBeRestored is null)
            return false;

        var configurationResult = await pipeClient.GetSystemConfiguration(cancellationToken);
        if (configurationResult?.Status != OperationStatus.Success || configurationResult.Configuration is null)
            return false;

        var current = configurationResult.Configuration;

        // configuration that modifies linux netplan config can trigger restart of network interface
        // that will automatically trigger suite restart
        if (current.NetworkDNSSettings.Equals(toBeRestored.NetworkDNSSettings) &&
            current.NetworkInterfacesSettings.Equals(toBeRestored.NetworkInterfacesSettings) &&
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Apply system configuration version='{Version}' from backup archive (network change:{Change}) correlated by {CorrelationId}")]
    private static partial void LogApplySystemConfigurationFromBackup(ILogger logger, Guid correlationId, int version, bool change);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process backup restore correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete uploaded backup file '{BackupFilePath}' correlated by {CorrelationId}")]
    private static partial void LogDeleteUploadedBackupFileFailed(ILogger logger, Exception exception, Guid correlationId, string backupFilePath);
}
