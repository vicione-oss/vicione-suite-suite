using Blazor.Server.Backend.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Xunit;

namespace Blazor.Server.Tests.Services;

public class UiEventPublisherTests
{
    [Fact]
    public async Task Publish_ui_event_successful()
    {
        // Arrange
        var logger = Substitute.For<ILogger<UiEventPublisher<FooEvent>>>();
        var publisher = new UiEventPublisher<FooEvent>(logger);

        var fooEvent = new FooEvent();
        var correlationId = Guid.NewGuid();
        var clientContext = new ClientContext<FooEvent>(fooEvent, correlationId);
        var handler = Substitute.For<IEventConsumer<FooEvent>>();

        // Act
        publisher.Connect(handler);
        await publisher.PublishUiEvent(fooEvent, correlationId);

        // Assert
        _ = handler.Received().Consume(clientContext, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_ui_event_consume_failed_exception_suppressed()
    {
        // Arrange
        var logger = Substitute.For<ILogger<UiEventPublisher<FooEvent>>>();
        var publisher = new UiEventPublisher<FooEvent>(logger);

        var fooEvent = new FooEvent();
        var correlationId = Guid.NewGuid();
        var clientContext = new ClientContext<FooEvent>(fooEvent, correlationId);

        var handler = Substitute.For<IEventConsumer<FooEvent>>();
        handler.When(m => m.Consume(Arg.Any<ClientContext<FooEvent>>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new ConsumerException("Consume failed."));

        // Act
        publisher.Connect(handler);
        await publisher.PublishUiEvent(fooEvent, correlationId);

        // Assert
        _ = handler.Received().Consume(clientContext, Arg.Any<CancellationToken>());
    }

    public sealed record FooEvent : IEvent;
}
