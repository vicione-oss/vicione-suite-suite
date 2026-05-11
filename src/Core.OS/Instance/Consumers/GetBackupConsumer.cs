using Core.Shared.Persistence.Requests;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class GetBackupConsumer(IBackupStore backupStore, ILogger<GetBackupConsumer> logger) : RequestConsumer<GetBackup, GetBackupResponse>
{
    public override async Task<GetBackupResponse> Respond(GetBackup message, CancellationToken cancellationToken)
    {
        var fileName = message.FileName;

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = backupStore.GetLatestBackup().Filename;

            LogUseLatestBackup(logger, fileName);
        }

        var backupStream = backupStore.ReadBackupFile(fileName);
        using var memoryStream = new MemoryStream();
        await backupStream.CopyToAsync(memoryStream, cancellationToken);

        return new GetBackupResponse(GetResponseFileName(fileName), memoryStream.ToArray());
    }

    public override Task<GetBackupResponse> HandleException(GetBackup message, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e, message.FileName);

        return Task.FromResult(new GetBackupResponse(string.Empty, [], new(0, e.Message)));
    }

    public static string GetResponseFileName(string fileName)
        => $"{Environment.MachineName}-{fileName}";

    [LoggerMessage(Level = LogLevel.Debug, Message = "Use latest backup='{FileName}'")]
    private static partial void LogUseLatestBackup(ILogger<GetBackupConsumer> logger, string? fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get backup='{FileName}'")]
    private static partial void LogError(ILogger<GetBackupConsumer> logger, Exception ex, string? fileName);
}
