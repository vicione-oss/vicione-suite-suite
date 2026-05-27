using System.IO.Abstractions;
using Core.OS.Modules;
using Core.Shared.Persistence.Contracts;

namespace Core.OS.Instance.Services;

internal class BackupFactory(IServiceProvider services) : IBackupFactory
{
    public Task<BackupSummary> CreateBackup(Stream destinationStream, CancellationToken cancellationToken)
    {
        var instanceInformationProvider = services.GetRequiredService<ILocalInstanceInformationProvider>();
        var fileSystem = services.GetRequiredService<IFileSystem>();
        var logger = services.GetRequiredService<ILogger<BackupBuilder>>();

        var workspaceManagement = services.GetRequiredService<IWorkspaceManagement>();
        var metadataProvider = services.GetRequiredService<IModuleMetadataProvider>();

        var backupBuilder = new BackupBuilder(fileSystem, instanceInformationProvider, logger)
                .UseModuleBackup(metadataProvider, workspaceManagement)
                .UseSystemConfigurationBackup(services);

        return backupBuilder.BuildBackup(destinationStream, cancellationToken);
    }
}
