using Core.OS.Instance.Extensions;
using Core.Shared.Persistence.Commands;
using Core.Shared.Persistence.Contracts;
using Core.Shared.Persistence.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed class CreateBackupConsumer(IBackupFactory backupFactory, IBackupStore backupStore, ILogger<CreateBackupConsumer> logger) : IConsumer<CreateBackup>
{
    public async Task Consume(ConsumeContext<CreateBackup> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        try
        {
            logger.LogInformation("Creating suite backup");

            // we write directly to the stream but know of errors only afterwards
            await using var fileStream = backupStore.CreateBackupFile(out var filename);
            var summary = await backupFactory.CreateBackup(fileStream, context.CancellationToken);
            var errors = summary.GetErrorMessages().ToArray();

            if (!errors.Any())
            {
                LogSummary(summary);
                await context.Publish(new BackupFinished(correlationId, filename, null));
                return;
            }

            if (context.Message.KeepBackupOnError)
            {
                logger.LogWarning("Backup '{FileName}' created with errors", filename);
                return;
            }

            foreach (var error in errors)
                logger.LogWarning("Backup error: {Error}", error);

            logger.LogWarning("Backup '{FileName}' created with errors and will be removed", filename);
            backupStore.DeleteBackupFile(filename);
        }
        catch (Exception e)
        {
            // cleanup store on failure?
            await context.Publish(new BackupFinished(correlationId, null, new ErrorInfo(100, e.Message)));
        }
    }

    private void LogSummary(BackupSummary summary)
    {
        logger.LogInformation("Backup created for instance {InstanceId} ({Type}) v{SuiteVersion} (Sdk v{SdkVersion})"
            , summary.InstanceId, summary.InstanceType, summary.SuiteVersion, summary.SdkVersion);

        if (summary.Modules.Count > 0)
        {
            logger.LogInformation("Backup contains modules {Modules}",
                string.Join(", ", summary.Modules.Select(k => $"{k.Name}:{k.ArchiveLength}bytes")));
        }
        else
        {
            logger.LogWarning("Backup contains no module data");
        }

        if (summary.SystemConfiguration is not null)
        {
            logger.LogInformation("Backup contains system configuration {Length}bytes.", summary.SystemConfiguration.ArchiveLength);
        }
        else
        {
            logger.LogWarning("Backup contains no system configuration");
        }
    }

}
