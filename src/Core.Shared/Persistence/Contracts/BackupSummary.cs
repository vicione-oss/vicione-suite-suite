namespace Core.Shared.Persistence.Contracts;

public class BackupSummary
{
    public required Guid InstanceId { get; init; }

    public required string SuiteVersion { get; init; }

    public required string SdkVersion { get; init; }

    public required string InstanceType { get; init; }

    public string? Name { get; init; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public BackupModuleSummary? SystemModule { get; set; }

    public BackupEntrySummary? SystemConfiguration { get; set; }

    public List<BackupModuleSummary> Modules { get; } = [];

    public string? Error { get; set; }
}

