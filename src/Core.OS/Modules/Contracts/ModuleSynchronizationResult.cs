using Sdk.Messaging;

namespace Core.OS.Modules.Contracts;

internal record ModuleSynchronizationResult(string Name)
{
    public string? Version { get; set; }

    public string? RelativeFolder { get; set; }

    public ErrorInfo? Error { get; set; }
}
