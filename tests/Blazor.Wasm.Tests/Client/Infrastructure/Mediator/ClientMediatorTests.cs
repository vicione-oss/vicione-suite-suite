using System.Text.Json;
using Blazor.Wasm.Client.Infrastructure.Mediator;
using Blazor.Wasm.Client.Infrastructure.SignalR;
using Bunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Requests;
using Sdk.Messaging;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Wasm.Tests.Client.Infrastructure.Mediator;

public class ClientMediatorTests
{
    private readonly IClientMessageHub _messageHubMock = Substitute.For<IClientMessageHub>();
    private readonly ILogger<UiMediator> _loggerMock = Substitute.For<ILogger<UiMediator>>();

    private UiMediator CreateMediator()
        => new(_messageHubMock, _loggerMock);

    [Fact]
    public async Task Send_request_should_return_response()
    {
        // Arrange
        var message = new GetConnections(null, null);
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();

        _messageHubMock.SendRequest(Arg.Any<SignalRMessageEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(new SignalRMessageEnvelope(
                typeof(GetConnectionsResponse).FullName,
                JsonSerializer.Serialize(message, message.GetType(), DefaultJsonSerializerSettings.Default)));

        using var mediator = CreateMediator();

        // Act
        var result = await mediator.Request<GetConnections, GetConnectionsResponse>(message);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Received_event_should_call_all_registered_consumers()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var message = new ConnectionChanged(correlationId, CrudAction.Created, new Connection(), [], []);
        var handler1Mock = Substitute.For<IEventConsumer<ConnectionChanged>>();
        var handler2Mock = Substitute.For<IEventConsumer<ConnectionChanged>>();

        using var ctx = new TestContext();
        ctx.SetupSuiteServices();

        using var mediator = CreateMediator();
        mediator.Register(handler1Mock);
        mediator.Register(handler2Mock);

        // Act
        _ = SignalRMessageFactory.Envelop(message);
        _messageHubMock.EventReceived += Raise.Event<EventHandler<SignalRMessageEnvelope>>(
            this, SignalRMessageFactory.Envelop(message));

        // Assert
        handler1Mock.Received(1).Consume(Arg.Any<ClientContext<ConnectionChanged>>(), Arg.Any<CancellationToken>());
        handler2Mock.Received(1).Consume(Arg.Any<ClientContext<ConnectionChanged>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Received_event_should_not_call_consumers_of_other_types()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var message = new ConnectionErrorOccured(correlationId, new ErrorInfo(0, null), connectionId);
        var handler1Mock = Substitute.For<IEventConsumer<ConnectionChanged>>();

        using var ctx = new TestContext();
        ctx.SetupSuiteServices();

        using var mediator = CreateMediator();
        mediator.Register(handler1Mock);

        // Act
        _messageHubMock.EventReceived += Raise.Event<EventHandler<SignalRMessageEnvelope>>(
            this, SignalRMessageFactory.Envelop(message));

        // Assert
        handler1Mock.Received(0).Consume(Arg.Any<ClientContext<ConnectionChanged>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Received_event_should_pass_correlation_id_to_consumer()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var message = new ConnectionChanged(correlationId, CrudAction.Created, new Connection(), [], []);
        var handler1Mock = Substitute.For<IEventConsumer<ConnectionChanged>>();

        using var ctx = new TestContext();
        ctx.SetupSuiteServices();

        using var mediator = CreateMediator();
        mediator.Register(handler1Mock);

        // Act
        var envelope = SignalRMessageFactory.Envelop(message, Guid.NewGuid());
        _messageHubMock.EventReceived -= Raise.Event<EventHandler<SignalRMessageEnvelope>>(
            this, envelope);

        // Assert
        handler1Mock.Received().Consume(
            Arg.Is<ClientContext<ConnectionChanged>>(context => context.CorrelationId == envelope.CorrelationId),
            Arg.Any<CancellationToken>());
    }
}
