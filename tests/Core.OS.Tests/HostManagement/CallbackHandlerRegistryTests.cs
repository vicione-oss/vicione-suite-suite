using Core.OS.HostManagement.Extensions;
using Core.OS.HostManagement.Handlers;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Communication.NamedPipe.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Core.OS.Tests.HostManagement;

public class CallbackHandlerRegistryTests
{
    private static ServiceProvider BuildServiceProvider(IPipeEventSubscriber handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(handler);
        services.AddPipeClient(new HostManagementOptions
        {
            MockClient = new MockPipeClientOptions
            {
                Enabled = true,
                DataSource = MockPipeClientDataSource.SystemConfigurationEmbedded
            }
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_route_event_to_registered_handler()
    {
        // Arrange
        var handler = new RecordingCallbackHandler();
        using var serviceProvider = BuildServiceProvider(handler);
        var registry = serviceProvider.GetRequiredService<CallbackHandlerRegistry>();

        // Act
        var handled = await registry.HandleCallbackAsync(
            new NamedPipeMessage { Topic = RecordingCallbackHandler.Topic, Type = MessageType.Event, Content = "content" },
            TestContext.Current.CancellationToken);

        // Assert
        handled.Should().BeTrue();
        handler.ReceivedContent.Should().Be("content");
    }

    [Fact]
    public async Task Should_not_handle_event_without_registered_handler()
    {
        // Arrange
        var handler = new RecordingCallbackHandler();
        using var serviceProvider = BuildServiceProvider(handler);
        var registry = serviceProvider.GetRequiredService<CallbackHandlerRegistry>();

        // Act
        var handled = await registry.HandleCallbackAsync(
            new NamedPipeMessage { Topic = "topic-without-handler", Type = MessageType.Event },
            TestContext.Current.CancellationToken);

        // Assert
        handled.Should().BeFalse();
        handler.ReceivedContent.Should().BeNull();
    }

    private sealed class RecordingCallbackHandler : IPipeEventSubscriber
    {
        public const string Topic = "recorded-topic";

        public string? ReceivedContent { get; private set; }

        public void RegisterWith(CallbackHandlerRegistry registry)
            => registry.RegisterRawHandler(Topic, Handle);

        private Task Handle(string messageContent, Guid correlationId, CancellationToken cancellationToken)
        {
            ReceivedContent = messageContent;
            return Task.CompletedTask;
        }
    }
}
