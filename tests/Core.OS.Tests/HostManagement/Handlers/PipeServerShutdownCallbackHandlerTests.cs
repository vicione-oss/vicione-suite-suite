using Core.OS.HostManagement.Extensions;
using Core.OS.HostManagement.Handlers;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Communication.NamedPipe.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.OS.Tests.HostManagement.Handlers;

public class PipeServerShutdownCallbackHandlerTests
{
    private readonly HostManagementOptions _options = new()
    {
        MockClient = new MockPipeClientOptions
        {
            Enabled = true,
            DataSource = MockPipeClientDataSource.SystemConfigurationEmbedded
        }
    };

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(_options));
        services.AddSingleton<IPipeEventSubscriber, PipeServerShutdownCallbackHandler>();
        services.AddPipeClient(_options);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_be_invoked_for_the_pipe_server_shutdown_event()
    {
        // Arrange
        await using var serviceProvider = BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<CallbackHandlerRegistry>();

        // Act
        var handled = await registry.HandleCallbackAsync(
            new NamedPipeMessage
            {
                Topic = EventTopics.PipeServerShutdown,
                Type = MessageType.Event,
                Content = "Service is stopping."
            },
            TestContext.Current.CancellationToken);

        // Assert
        handled.Should().BeTrue();
    }
}
