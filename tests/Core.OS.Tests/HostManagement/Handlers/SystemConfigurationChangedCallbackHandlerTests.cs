using System.Text.Json;
using Core.OS.HostManagement;
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

public class SystemConfigurationChangedCallbackHandlerTests
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
        services.AddSingleton<SystemConfigurationCache>();
        services.AddSingleton<IPipeEventSubscriber, SystemConfigurationChangedCallbackHandler>();
        services.AddPipeClient(_options);

        return services.BuildServiceProvider();
    }

    private SystemConfigurationCache GetCacheWithConfiguration(ServiceProvider serviceProvider)
    {
        var cache = serviceProvider.GetRequiredService<SystemConfigurationCache>();
        cache.Set(TestPipeClient.GetEmbeddedSystemConfiguration());

        return cache;
    }

    private static GetSystemConfigurationResult CreateChangedConfigurationResult()
        => GetSystemConfigurationResult.CreateSuccessResult("Configuration changed.", TestPipeClient.GetEmbeddedSystemConfiguration());

    [Fact]
    public async Task Should_invalidate_cached_system_configuration()
    {
        // Arrange
        await using var serviceProvider = BuildServiceProvider();
        var cache = GetCacheWithConfiguration(serviceProvider);
        var handler = ActivatorUtilities.CreateInstance<SystemConfigurationChangedCallbackHandler>(serviceProvider);

        // Act
        await handler.HandleAsync(CreateChangedConfigurationResult(), Guid.Empty, TestContext.Current.CancellationToken);

        // Assert
        cache.Get().Should().BeNull();
    }

    [Fact]
    public async Task Should_be_invoked_for_the_host_management_configuration_changed_event()
    {
        // Arrange
        await using var serviceProvider = BuildServiceProvider();
        var cache = GetCacheWithConfiguration(serviceProvider);
        var registry = serviceProvider.GetRequiredService<CallbackHandlerRegistry>();

        // Act
        var handled = await registry.HandleCallbackAsync(
            new NamedPipeMessage
            {
                Topic = EventTopics.SystemConfigurationChanged,
                Type = MessageType.Event,
                Content = JsonSerializer.Serialize(CreateChangedConfigurationResult(), SourceGenerationContext.Default.GetSystemConfigurationResult)
            },
            TestContext.Current.CancellationToken);

        // Assert
        handled.Should().BeTrue();
        cache.Get().Should().BeNull();
    }
}
