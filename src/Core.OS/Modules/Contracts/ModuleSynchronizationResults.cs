namespace Core.OS.Modules.Contracts;

internal class ModuleSynchronizationResults
{
    public Exception? HttpResolveError { get; set; }

    public List<ModuleSynchronizationResult> Incompatible { get; } = [];

    public List<ModuleSynchronizationResult> Resolved { get; } = [];

    public List<ModuleSynchronizationResult> Deleted { get; } = [];

    public List<ModuleSynchronizationResult> Updated { get; } = [];

    public List<ModuleSynchronizationResult> UpdateFailed { get; } = [];

    public List<ModuleSynchronizationResult> UpdateSkipped { get; } = [];

    public List<ModuleSynchronizationResult> All =>
        [.. Incompatible, .. Resolved, .. Deleted, .. Updated, .. UpdateFailed];
}
