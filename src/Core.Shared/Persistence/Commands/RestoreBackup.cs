using Sdk.Messaging;

namespace Core.Shared.Persistence.Commands;

public class RestoreBackup : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    public bool SuiteConfiguration { get; init; }

    public bool SystemConfiguration { get; init; }

    public required IReadOnlyCollection<byte> BackupFileContent { get; init; }
}
