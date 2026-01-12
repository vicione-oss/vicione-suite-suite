using Blazor.Wasm.Client.Infrastructure.SignalR;
using Core.Shared.Connections.Commands;
using Core.Shared.Instance.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Connections.Contracts;
using Sdk.Connections.Requests;
using Sdk.Messaging;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Infrastructure.SignalR;

public class SignalRMessageFactoryTests
{
    [Fact]
    public void Envelope_event_should_work()
    {
        // Arrange
        var message = new InstanceHealthChangedEvent(new HealthStatus(), new());

        // Act
        var envelope = SignalRMessageFactory.Envelop(message);

        // Assert
        Assert.NotNull(envelope);
        Assert.Null(envelope.CorrelationId);    // event is not correlated by default
    }

    [Fact]
    public void Envelope_command_should_work()
    {
        // Arrange
        var message = new TestConnection(Guid.NewGuid(), new Connection());

        // Act
        var envelope = SignalRMessageFactory.Envelop(message);

        // Assert
        Assert.NotNull(envelope);
    }

    [Fact]
    public void Envelope_request_should_work()
    {
        // Arrange
        var message = new GetConnections(Guid.NewGuid(), null);

        // Act
        var envelope = SignalRMessageFactory.Envelop(message);

        // Assert
        Assert.NotNull(envelope);
    }

    [Fact]
    public void Correlation_id_should_be_taken_from_message_if_possible()
    {
        // Arrange
        var correlated = new TestCommandCorrelatedBy(Guid.NewGuid());
        var withEventId = new TestCommandWithEventId(Guid.NewGuid());

        // Act
        var correlatedEnvelope = SignalRMessageFactory.Envelop(correlated);
        var withEventIdEnvelope = SignalRMessageFactory.Envelop(withEventId);

        // Assert
        Assert.NotNull(correlatedEnvelope);
        Assert.NotNull(withEventIdEnvelope);

        Assert.Equal(correlated.CorrelationId, correlatedEnvelope.CorrelationId);
        Assert.Equal(withEventId.EventId, withEventIdEnvelope.CorrelationId);
    }

    private record TestCommandCorrelatedBy(Guid CorrelationId) : ICommand;
    private record TestCommandWithEventId(Guid EventId) : IEvent;
}
