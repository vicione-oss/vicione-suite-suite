using AwesomeAssertions;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Service;
using HostManagement.Shared.Enums;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.SystemConfiguration.Events;
using Sdk.Testing.Backend;
using Xunit;
using OperationStatus = HostManagement.Shared.Communication.Enums.OperationStatus;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class SetSystemConfigurationConsumerTests
{
    private static void ConfigureServices(IBusRegistrationConfigurator cfg, IPipeClient? pipeClient = null)
    {
        cfg.AddConsumer<SetSystemConfigurationConsumer>();
        cfg.AddSingleton<SystemConfigurationCache>();

        if (pipeClient is not null)
        {
            cfg.AddSingleton(pipeClient);
            return;
        }

        cfg.AddMockPipeClientSystemConfiguration();
    }

    [Fact]
    public async Task Should_publish_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => ConfigureServices(cfg));

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
        (await tester.Harness.Published.Any<SystemConfigurationChanged>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_error_event_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => ConfigureServices(cfg, Substitute.For<IPipeClient>()));

        var config = new SystemConfiguration();
        var command = new SetSystemConfiguration(config);

        // Act
        await tester.TestCommand<SetSystemConfiguration, SetSystemConfigurationConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<SetSystemConfigurationError>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_error_event_when_pipe_client_returns_error()
    {
        // Arrange
        var pipeClient = Substitute.For<IPipeClient>();
        pipeClient.SetupGetSystemConfigurationResult(OperationStatus.Success);
        pipeClient.SetupSetSystemConfigurationResult(OperationStatus.Error, "Test error");

        await using var tester = new MassTransitTester(cfg => ConfigureServices(cfg, pipeClient));

        var config = new SystemConfiguration();
        var command = new SetSystemConfiguration(config);

        // Act
        await tester.TestCommand<SetSystemConfiguration, SetSystemConfigurationConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<SetSystemConfigurationError>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_publish_restart_required_when_proxy_settings_are_null()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => ConfigureServices(cfg));

        var config = new SystemConfiguration
        {
            NetworkProxySettings = new(),
            Services =
            [
                new ServiceDetail
                {
                    Name = "Service1",
                    State = ServiceState.Enabled,
                }
            ]
        };
        var command = new SetSystemConfiguration(config);

        // Act
        await tester.TestCommand<SetSystemConfiguration, SetSystemConfigurationConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<SystemRestartRequired>(TestContext.Current.CancellationToken)).Should().BeFalse();
        (await tester.Harness.Published.Any<SystemConfigurationChanged>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }
}
