using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Contracts;

public sealed class SyncError
{
    public SyncErrorType Type { get; set; }
    public Connection? Connection { get; set; }
}
