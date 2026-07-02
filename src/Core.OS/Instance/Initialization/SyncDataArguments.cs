using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.Instance.Initialization;

[MessageEndpoint("InitialSync")]
public sealed record SyncDataArguments : IInstanceDependentActivityArgument
{
    public string ModuleId { get; init; } = string.Empty;
    public string DbContextTypeName { get; init; } = string.Empty;
    public string Table { get; init; } = string.Empty;
    public string ColumnsCsv { get; init; } = string.Empty;
    public List<string> ValuesCsv { get; init; } = [];
    public bool SyncCompleted { get; init; }
}
