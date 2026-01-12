using Core.Shared.Persistence.Contracts;

namespace Core.OS.Instance;

public interface IBackupFactory
{
    Task<BackupSummary> CreateBackup(Stream destinationStream, CancellationToken cancellationToken);
}
