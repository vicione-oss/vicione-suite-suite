namespace Core.OS.Instance.Contracts;

public class ModuleBackupMetadata
{
    public required string EntryName { get; init; }

    public required string Name { get; init; }

    public string? Version { get; init; }

    public string? Title { get; init; }
}
