using Core.Shared.Persistence.Contracts;

namespace Core.OS.Instance.Services;

internal class BackupFactory(IServiceProvider services) : IBackupFactory
{
    public Task<BackupSummary> CreateBackup(Stream destinationStream, CancellationToken cancellationToken)
    {
        var backupBuilder = new BackupBuilder()
                .UseModuleBackup()
                .UseSystemConfigurationBackup();

        return backupBuilder.BuildBackup(services, destinationStream, cancellationToken);
    }
}
