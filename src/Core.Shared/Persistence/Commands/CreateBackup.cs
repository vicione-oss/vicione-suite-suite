using Sdk.Messaging;

namespace Core.Shared.Persistence.Commands;

public class CreateBackup : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    public bool KeepBackupOnError { get; init; }
}
