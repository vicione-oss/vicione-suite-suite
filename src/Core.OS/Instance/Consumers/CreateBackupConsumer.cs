using System.Globalization;
using System.Text;
using Core.OS.Instance.Extensions;
using Core.Shared.Persistence.Commands;
using Core.Shared.Persistence.Contracts;
using Core.Shared.Persistence.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class CreateBackupConsumer(IBackupFactory backupFactory, IBackupStore backupStore, ILogger<CreateBackupConsumer> logger) : IConsumer<CreateBackup>
{
    public async Task Consume(ConsumeContext<CreateBackup> context)
    {
        var correlationId = context.Message.CorrelationId;

        LogConsume(logger, correlationId, context.Message.KeepBackupOnError);

        try
        {
            // we write directly to the stream but know of errors only afterwards
            await using var fileStream = backupStore.CreateBackupFile(out var filename);
            var summary = await backupFactory.CreateBackup(fileStream, context.CancellationToken);

            var errors = summary.GetErrorMessages().ToArray();
            if (errors.Length == 0)
            {
                LogBackupSummary(correlationId, summary);
                await context.Publish(new BackupFinished(correlationId, filename));
                return;
            }

            if (context.Message.KeepBackupOnError)
            {
                LogKeepBackupWithErrors(logger, correlationId, filename);
                return;
            }

            backupStore.DeleteBackupFile(filename);

            LogDeleteFailedBackup(logger, correlationId, filename);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId);

            // cleanup store on failure?
            await context.Publish(new BackupFinished(correlationId, null, new ErrorInfo(100, ex.Message)));
        }
    }

    private void LogBackupSummary(Guid correlationId, BackupSummary summary)
    {
        if (!logger.IsEnabled(LogLevel.Information))
            return;

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Backup summary for instance {summary.InstanceId} ({summary.InstanceType}) v{summary.SuiteVersion} (Sdk v{summary.SdkVersion}) correlated by {correlationId}:");

        if (summary.Modules.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"Modules: {string.Join(", ", summary.Modules.Select(k => $"{k.Name}:{k.ArchiveLength} bytes"))}.");
        }
        else
        {
            sb.AppendLine("Modules: no module data");
        }

        if (summary.SystemConfiguration is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"System: Backup contains configuration {summary.SystemConfiguration.ArchiveLength} bytes.");
        }
        else
        {
            sb.AppendLine("System: no configuration data");
        }

        logger.LogInformation("{BackupSummary}", sb.ToString());
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume create backup correlated by {CorrelationId}. Keep on error={KeepBackupOnError}")]
    private static partial void LogConsume(ILogger<CreateBackupConsumer> logger, Guid correlationId, bool keepBackupOnError);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delete failed backup file='{FileName}' correlated by {CorrelationId}")]
    private static partial void LogDeleteFailedBackup(ILogger<CreateBackupConsumer> logger, Guid correlationId, string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Keep backup file='{FileName}' with errors correlated by {CorrelationId} ")]
    private static partial void LogKeepBackupWithErrors(ILogger<CreateBackupConsumer> logger, Guid correlationId, string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create backup correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<CreateBackupConsumer> logger, Exception exception, Guid correlationId);
}
