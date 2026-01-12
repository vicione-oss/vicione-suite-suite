namespace Core.Shared.Persistence.Contracts;

public class BackupModuleSummary : BackupEntrySummary
{
    public List<BackupEntrySummary>? Databases { get; set; }
}
