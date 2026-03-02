using Core.Shared.Persistence.Requests;
using MassTransit;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed class GetBackupConsumer(IBackupStore backupStore, ILogger<GetBackupConsumer> logger) : RequestConsumer<GetBackup, GetBackupResponse>
{
    protected override async Task<GetBackupResponse> Respond(ConsumeContext<GetBackup> context)
    {
        var fileName = context.Message.FileName;

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = backupStore.GetLatestBackup().Filename;

            logger.LogDebug("Use latest backup {File} on request '{Request}'", fileName, nameof(GetBackup));
        }

        var backupStream = backupStore.ReadBackupFile(fileName);
        using var memoryStream = new MemoryStream();
        await backupStream.CopyToAsync(memoryStream, context.CancellationToken);

        return new GetBackupResponse(GetResponseFileName(fileName), memoryStream.ToArray());
    }

    protected override Task<GetBackupResponse> HandleException(ConsumeContext<GetBackup> context, Exception e)
    {
        logger.LogError(e, $"Failed to handle {nameof(GetBackup)}");

        return Task.FromResult(new GetBackupResponse(string.Empty, [], new(0, e.Message)));
    }

    public static string GetResponseFileName(string fileName)
        => $"{Environment.MachineName}-{fileName}";
}
