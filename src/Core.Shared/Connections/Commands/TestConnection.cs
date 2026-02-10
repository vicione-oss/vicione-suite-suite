using Sdk.Connections.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Connections.Commands;

public sealed record TestConnection(Guid RequestId, Connection Connection) : ICommand
{
    public Guid CorrelationId { get; init; } = RequestId;
}
