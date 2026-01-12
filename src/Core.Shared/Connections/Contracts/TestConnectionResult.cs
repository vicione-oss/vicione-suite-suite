using Sdk.Messaging;

namespace Core.Shared.Connections.Contracts;

public sealed class TestConnectionResult
{
    public Guid? ConnectionId { get; set; }
    public bool Success => ErrorInfo is null;
    public ErrorInfo? ErrorInfo { get; set; }
}
