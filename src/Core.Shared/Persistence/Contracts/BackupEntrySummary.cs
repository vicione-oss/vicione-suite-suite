namespace Core.Shared.Persistence.Contracts;

/// <summary>
/// A single archive entry within <see cref="BackupSummary"/>.
/// </summary>
public class BackupEntrySummary
{
    public required string EntryName { get; set; }

    public required string Name { get; init; }

    public string? Version { get; init; }

    public long ArchiveLength { get; set; }

    public string? Error { get; set; }
}
