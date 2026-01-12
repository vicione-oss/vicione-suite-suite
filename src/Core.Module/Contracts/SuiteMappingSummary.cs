namespace Core.Module.Contracts;

internal class SuiteMappingSummary
{
    public int TotalMappings { get; init; }

    public required List<ModuleMappingSummary> Modules { get; init; }
}
