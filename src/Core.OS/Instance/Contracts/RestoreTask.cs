namespace Core.OS.Instance.Contracts;

public record RestoreTask(string BackupPath, bool RestoreSuiteConfig, bool RestoreSystemConfig, DateTimeOffset Created);
