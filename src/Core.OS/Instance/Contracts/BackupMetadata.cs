using Sdk.Instance;

namespace Core.OS.Instance.Contracts;

/// <summary>
/// Metadata that gets stored within the backup archive.
/// Ensure to add some migration code if you change this class
/// </summary>
public class BackupMetadata
{
    public required Guid InstanceId { get; init; }

    public required string SuiteVersion { get; init; }

    public required string SdkVersion { get; init; }

    public required InstanceType Type { get; init; }

    public string? Name { get; init; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ModuleBackupMetadata> Modules { get; set; } = [];
}
