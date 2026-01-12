using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.SystemConfiguration.Events;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class SystemConfigurationChangedConsumerTests
{
    [Fact]
    public async Task Event_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<SystemConfigurationChangedConsumer>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationChangedConsumer>>());
            cfg.AddSingleton<SystemConfigurationCache>();
            cfg.AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>());
        });
        var logger = tester.Services.GetRequiredService<ILogger<SystemConfigurationChangedConsumer>>();
        var changeEvent = new SystemConfigurationChanged(Guid.NewGuid());

        // Act
        await tester.TestEvent<SystemConfigurationChanged, SystemConfigurationChangedConsumer>(changeEvent);

        // Assert
        logger.Received()
            .Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("configuration changed")),
                null,
                Arg.Any<Func<object, Exception?, string>>()
            );
    }
}
