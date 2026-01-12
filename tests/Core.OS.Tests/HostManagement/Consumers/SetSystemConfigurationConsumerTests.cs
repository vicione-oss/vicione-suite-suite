using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using AwesomeAssertions;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Service;
using HostManagement.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.SystemConfiguration.Events;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class SetSystemConfigurationConsumerTests
{
    [Fact]
    public async Task Should_publish_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<SetSystemConfigurationConsumer>();
            cfg.AddMockPipeClientSystemConfiguration();
        });

        var config = new SystemConfiguration
        {
            Services =
            [
                new ServiceDetail
                {
                    Name = "Service1",
                    State = ServiceState.Unknown,
                },
                new ServiceDetail
                {
                    Name = "Service2",
                    State = ServiceState.Enabled,
                },
            ]
        };
        var command = new SetSystemConfiguration(config);

        // Act
        await tester.TestCommand<SetSystemConfiguration, SetSystemConfigurationConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<SystemConfigurationChanged>()).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_error_event_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<SetSystemConfigurationConsumer>();
            cfg.AddSingleton(Substitute.For<IPipeClient>());
        });

        var config = new SystemConfiguration();
        var command = new SetSystemConfiguration(config);

        // Act
        await tester.TestCommand<SetSystemConfiguration, SetSystemConfigurationConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<SetSystemConfigurationError>()).Should().BeTrue();
    }
}
