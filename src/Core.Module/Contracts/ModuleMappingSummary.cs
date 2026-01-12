namespace Core.Module.Contracts;

internal sealed class ModuleMappingSummary
{
    public required string AssemblyName { get; init; }

    public required ModuleMappingVersion MapTo { get; init; }

    public required List<ModuleMappingVersion> Matches { get; init; }

    public required List<ModuleMappingVersion> BuildDiffs { get; init; }

    public required List<ModuleMappingVersion> MinorDiffs { get; init; }

    public required List<ModuleMappingVersion> MajorDiffs { get; init; }

    public int MismatchCount => BuildDiffs.Count + MinorDiffs.Count + MajorDiffs.Count;
}
