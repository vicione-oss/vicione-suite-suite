using Sdk.Messaging;
using Semver;

namespace Core.OS.Modules.Contracts;

internal record ModuleSynchronizationResult(string Name)
{
    public SemVersion? Version { get; set; }

    public string? RelativeFolder { get; set; }

    public ErrorInfo? Error { get; set; }
}
