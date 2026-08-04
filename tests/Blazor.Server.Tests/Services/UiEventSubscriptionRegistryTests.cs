using System.Security.Principal;
using Blazor.Server.Backend.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Server.Tests.Services;

public sealed class UiEventSubscriptionRegistryTests
{
    public sealed class Connect
    {
        [Fact]
        public void Should_return_disposable_handle()
        {
            // Arrange
            var sut = new UiEventSubscriptionRegistry<FooEvent>();
            var consumer = Substitute.For<IEventConsumer<FooEvent>>();

            // Act
            var subscription = sut.Connect(consumer);

            // Assert
            subscription.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_disconnect_consumer_when_disposed()
        {
            // Arrange
            var sut = new UiEventSubscriptionRegistry<FooEvent>();
            var consumer = Substitute.For<IEventConsumer<FooEvent>>();
            var subscription = sut.Connect(consumer);

            var handled = false;

            // Act
            subscription.Dispose();
            await sut.ForEachAsync((c, id) =>
            {
                handled = true;
                return Task.CompletedTask;
            });

            // Assert
            handled.Should().BeFalse("after disposing the returned IDisposable the consumer must be removed");
        }
    }

    public sealed class ForEachAsync
    {
        [Fact]
        public async Task Should_invoke_handler_for_all_connected_consumers_in_order_and_pass_identities()
        {
            // Arrange
            var sut = new UiEventSubscriptionRegistry<FooEvent>();

            var c1 = Substitute.For<IEventConsumer<FooEvent>>();
            var c2 = Substitute.For<IEventConsumer<FooEvent>>();

            var id1 = new GenericIdentity("alice@example.com");
            var id2 = (IIdentity?)null;

            sut.Connect(c1, id1);
            sut.Connect(c2, id2);

            var seen = new List<(IEventConsumer<FooEvent> consumer, IIdentity? id)>();

            // Act
            await sut.ForEachAsync((c, id) =>
            {
                seen.Add((c, id));
                return Task.CompletedTask;
            });

            // Assert
            seen.Should().HaveCount(2);
            seen[0].consumer.Should().Be(c1);
            seen[0].id.Should().Be(id1);
            seen[1].consumer.Should().Be(c2);
            seen[1].id.Should().BeNull();
        }

        [Fact]
        public async Task Should_await_async_handler()
        {
            // Arrange
            var sut = new UiEventSubscriptionRegistry<FooEvent>();
            var c = Substitute.For<IEventConsumer<FooEvent>>();
            sut.Connect(c);

            var started = false;
            var finished = false;

            // Act
            await sut.ForEachAsync(async (_, __) =>
            {
                started = true;
                await Task.Delay(10);
                finished = true;
            });

            // Assert
            started.Should().BeTrue();
            finished.Should().BeTrue();
        }

        [Fact]
        public async Task Should_handle_empty_registry_gracefully()
        {
            // Arrange
            var sut = new UiEventSubscriptionRegistry<FooEvent>();
            var calls = 0;

            // Act
            await sut.ForEachAsync((_, __) =>
            {
                calls++;
                return Task.CompletedTask;
            });

            // Assert
            calls.Should().Be(0);
        }
    }

    public sealed record FooEvent : IEvent;
}
