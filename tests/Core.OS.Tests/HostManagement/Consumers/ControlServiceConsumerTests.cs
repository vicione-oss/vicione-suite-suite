using Core.OS.HostManagement.Consumers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using Sdk.SystemConfiguration;
using Sdk.SystemConfiguration.Commands;
using Sdk.SystemConfiguration.Contracts;
using Sdk.SystemConfiguration.Events;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.HostManagement.Consumers;

public class ControlServiceConsumerTests
{
    private const string ServiceName = "ControlServiceTest";

    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IControlServiceManagement _serviceManagement = Substitute.For<IControlServiceManagement>();

    public ControlServiceConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<ControlServiceConsumer>();
            cfg.AddSingleton(_serviceManagement);
            cfg.AddSingleton(Substitute.For<ILogger<ControlServiceConsumer>>());
        };
    }

    [Fact]
    public async Task Should_call_control_service()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new ControlService(ServiceName, ServiceCommand.Start);

        // Act
        await tester.TestInstanceDependentCommand<ControlService, ControlServiceConsumer>(command);

        // Assert
        await _serviceManagement.Received().TryControlService(command.Command, command.ServiceName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_events_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new ControlService(ServiceName, ServiceCommand.Start);
        _serviceManagement.TryControlService(command.Command, command.ServiceName, Arg.Any<CancellationToken>())
            .Returns(new ControlServiceManagementResult(command.ServiceName, ServiceState.Enabled));

        // Act
        await tester.TestInstanceDependentCommand<ControlService, ControlServiceConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<SystemConfigurationChanged>(TestContext.Current.CancellationToken)).Should().BeTrue();
        (await tester.Harness.Published.Any<ControlServiceCompleted>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_send_error_event_on_failure()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new ControlService(ServiceName, ServiceCommand.Start);
        _serviceManagement.TryControlService(command.Command, command.ServiceName, Arg.Any<CancellationToken>())
            .Returns(new ControlServiceManagementResult(command.ServiceName, ServiceState.Enabled, new ErrorInfo(3, "Error occurred")));

        // Act
        await tester.TestInstanceDependentCommand<ControlService, ControlServiceConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlServiceCompleted>(r =>
            r.Context.Message.ErrorInfo != null, TestContext.Current.CancellationToken)).Should().BeTrue();
    }
}
