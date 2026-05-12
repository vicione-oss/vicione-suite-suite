using Sdk.Connections.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Connections.Commands;

public sealed record TestConnection(Guid CorrelationId, Connection Connection) : ICommand;
