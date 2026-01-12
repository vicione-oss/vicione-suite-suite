using Core.Shared.Persistence.Contracts;

namespace Core.OS.Instance;

public interface IBackupStore
{
    Stream CreateBackupFile(out string filename);

    /// <summary>
    /// If filename is null, the most recent backup file will be read.
    /// </summary>
    Stream ReadBackupFile(string? filename = null);

    void DeleteBackupFile(string filename);

    IReadOnlyCollection<BackupFileInfo> GetBackupFiles();

    BackupFileInfo GetLatestBackup();
}
