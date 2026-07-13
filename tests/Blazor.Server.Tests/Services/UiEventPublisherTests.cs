using System.Globalization;
using System.Security.Principal;
using AwesomeAssertions;
using Blazor.Server.Backend.Services;
using Blazor.Shared;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Xunit;

namespace Blazor.Server.Tests.Services;

public abstract class UiEventPublisherTests
{
    private readonly FakeUiEventSubscriptionRegistry<FooEvent> _registry = new();
    private readonly IMemoryCache _memoryCache = Substitute.For<IMemoryCache>();
    private readonly ILogger<UiEventPublisher<FooEvent>> _logger = Substitute.For<ILogger<UiEventPublisher<FooEvent>>>();

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_memoryCache)
            .AddSingleton(_logger)
            .AddSingleton<IUiEventSubscriptionRegistry<FooEvent>>(_registry)
            .AddSingleton<UiEventPublisher<FooEvent>>()
            .BuildServiceProvider();

    public sealed class Connect : UiEventPublisherTests
    {
        [Fact]
        public async Task Should_use_subscription_identity_if_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var publisher = serviceProvider.GetRequiredService<UiEventPublisher<FooEvent>>();

            var handler = Substitute.For<IEventConsumer<FooEvent>>();
            var identity = Substitute.For<IIdentity>();
            identity.Name.Returns("TestUser");

            // Act
            _ = publisher.Connect(handler, identity);

            // Assert
            _registry.HasHandler(handler).Should().BeTrue();
        }
    }

    public sealed class PublishUiEvent : UiEventPublisherTests
    {

        [Fact]
        public async Task Should_use_subscription_identity_if_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var publisher = serviceProvider.GetRequiredService<UiEventPublisher<FooEvent>>();

            var handler = Substitute.For<IEventConsumer<FooEvent>>();
            var identity = Substitute.For<IIdentity>();
            identity.Name.Returns("TestUser");
            var cacheKey = Constants.GetUserCultureCacheKey(identity.Name!);

            _registry.Add(handler, identity);

            // Act
            await publisher.PublishUiEvent(new FooEvent(), Guid.NewGuid(), TestContext.Current.CancellationToken);

            // Assert
            _memoryCache.Received(1).TryGetValue(cacheKey, out _);
        }

        [Fact]
        public async Task Should_invoke_each_consumer_set_culture_log_and_restore_default_culture()
        {
            // Arrange
            var defaultUi = new CultureInfo("de-DE");
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = defaultUi;

            using var serviceProvider = SetupServiceProvider();
            var publisher = serviceProvider.GetRequiredService<UiEventPublisher<FooEvent>>();
            var user = Substitute.For<IIdentity>();
            user.Name.Returns("TestUser");

            var consumerWithUser = Substitute.For<IEventConsumer<FooEvent>>();
            var consumerAnon = Substitute.For<IEventConsumer<FooEvent>>();
            _registry.Add(consumerWithUser, user);
            _registry.Add(consumerAnon, identity: null);

            var cacheKey = Constants.GetUserCultureCacheKey(user.Name!);
            _memoryCache
                .TryGetValue(cacheKey, out Arg.Any<object?>())
                .Returns(x =>
                {
                    x[1] = "en-GB";
                    return true;
                });

            var evt = new FooEvent();
            var correlation = Guid.NewGuid();

            // Act
            await publisher.PublishUiEvent(evt, correlation, TestContext.Current.CancellationToken);

            // Assert
            await consumerWithUser.Received(1).Consume(
                Arg.Is<ClientContext<FooEvent>>(c => c.Message == evt && c.CorrelationId == correlation),
                Arg.Any<CancellationToken>());

            await consumerAnon.Received(1).Consume(
                Arg.Is<ClientContext<FooEvent>>(c => c.Message == evt && c.CorrelationId == correlation),
                Arg.Any<CancellationToken>());

            CultureInfo.CurrentUICulture.Should().Be(defaultUi);
            CultureInfo.CurrentUICulture = previous;
        }

        [Fact]
        public async Task Should_suppress_exception_when_consume_fails()
        {
            // Arrange
            using var serviceProvider = SetupServiceProvider();
            var publisher = serviceProvider.GetRequiredService<UiEventPublisher<FooEvent>>();

            var fooEvent = new FooEvent();
            var correlationId = Guid.NewGuid();
            var clientContext = new ClientContext<FooEvent>(fooEvent, correlationId);

            var handler = Substitute.For<IEventConsumer<FooEvent>>();
            handler.When(m => m.Consume(Arg.Any<ClientContext<FooEvent>>(), Arg.Any<CancellationToken>()))
                .Do(_ => throw new ConsumerException("Consume failed."));

            _registry.Add(handler, null);

            // Act
            await publisher.PublishUiEvent(fooEvent, correlationId, TestContext.Current.CancellationToken);

            // Assert
            _ = handler.Received().Consume(clientContext, Arg.Any<CancellationToken>());
        }
    }

    public sealed record FooEvent : IEvent;

    private sealed class FakeUiEventSubscriptionRegistry<T> : IUiEventSubscriptionRegistry<T>
        where T : class, IEvent
    {
        private readonly List<(IEventConsumer<T> consumer, IIdentity? identity)> _items = [];

        public void Add(IEventConsumer<T> consumer, IIdentity? identity) => _items.Add((consumer, identity));

        public Task ForEachAsync(Func<IEventConsumer<T>, IIdentity?, Task> action) =>
            Task.Run(async () =>
            {
                foreach (var (consumer, id) in _items)
                    await action(consumer, id);
            });

        public IDisposable Connect(IEventConsumer<T> handler, IIdentity? identity = null)
        {
            Add(handler, identity);
            return Substitute.For<IDisposable>();
        }

        public bool HasHandler(IEventConsumer<T> consumer) => _items.Any(k => k.consumer == consumer);
    }
}
