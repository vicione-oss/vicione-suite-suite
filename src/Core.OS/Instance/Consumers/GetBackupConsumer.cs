using Core.Shared.Persistence.Requests;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed class GetBackupConsumer(IBackupStore backupStore, ILogger<GetBackupConsumer> logger) : RequestConsumer<GetBackup, GetBackupResponse>
{
    public override async Task<GetBackupResponse> Respond(GetBackup message, CancellationToken cancellationToken)
    {
        var fileName = message.FileName;

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = backupStore.GetLatestBackup().Filename;

            logger.LogDebug("Use latest backup {File} on request '{Request}'", fileName, nameof(GetBackup));
        }

        var backupStream = backupStore.ReadBackupFile(fileName);
        using var memoryStream = new MemoryStream();
        await backupStream.CopyToAsync(memoryStream, cancellationToken);

        return new GetBackupResponse(GetResponseFileName(fileName), memoryStream.ToArray());
    }

    public override Task<GetBackupResponse> HandleException(GetBackup message, Exception e, CancellationToken cancellationToken)
    {
        logger.LogError(e, $"Failed to handle {nameof(GetBackup)}");

        return Task.FromResult(new GetBackupResponse(string.Empty, [], new(0, e.Message)));
    }

    public static string GetResponseFileName(string fileName)
        => $"{Environment.MachineName}-{fileName}";
}
