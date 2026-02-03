using Sdk.Instance;
using Sdk.Messaging;

namespace Core.OS.Instance.Commands;

public sealed record RegisterInstance : ICommand
{
    public Guid InstanceId { get; init; }
    public InstanceType Type { get; init; }
    public string? Name { get; init; }
    public string? FormattedName { get; set; }
    public string? Description { get; init; }
    public string SerialNumber { get; init; } = string.Empty;
    public string SystemType { get; init; } = string.Empty;
    public string SdkVersion { get; init; } = string.Empty;
    public string? BranchName { get; init; }
    public List<string> InstalledModules { get; init; } = [];
    public List<KeyValuePair<string, string?>> Configuration { get; init; } = [];
    public string Version { get; init; } = string.Empty;
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
