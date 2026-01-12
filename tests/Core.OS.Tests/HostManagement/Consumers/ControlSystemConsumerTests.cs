using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.Instance.Contracts;
using AwesomeAssertions;
using HostManagement.Shared.Communication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;
using CommunicationEnums = HostManagement.Shared.Communication.Enums;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class ControlSystemConsumerTests
{
    private readonly IPipeClient _pipeClient = Substitute.For<IPipeClient>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly ILocalInstanceInformationProvider _instanceInformation = Substitute.For<ILocalInstanceInformationProvider>();

    private MassTransitTester SetupMassTransitTester()
        => new(cfg =>
        {
            var options = new InstanceOptions
            {
                HomeDirectory = _fileSystem.Path.GetFullPath("app"),
                CacheDirectory = _fileSystem.Path.GetFullPath("cache"),
                BackupDirectory = _fileSystem.Path.GetFullPath("backup"),
                Type = InstanceType.Standalone
            };

            _fileSystem.AddDirectory(options.HomeDirectory);

            cfg.AddConsumer<ControlSystemConsumer>();
            cfg.AddSingleton(Substitute.For<ILogger<ControlSystemConsumer>>());
            cfg.AddSingleton(_pipeClient);
            cfg.AddSingleton<IFileSystem>(_fileSystem);
            cfg.AddSingleton(_instanceInformation);
            cfg.AddSingleton(Options.Create(options));
        });

    [Fact]
    public async Task Should_send_reset_system_to_host_management()
    {
        // Arrange
        _pipeClient.SetupResetSystemResult(CommunicationEnums.OperationStatus.Success);

        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Reset);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemCompleted>()).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.ResetSystem, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_set_reset_flag_and_publish_shutdown_after_system_configuration_is_applied()
    {
        // Arrange
        _pipeClient.SetupResetSystemResult(CommunicationEnums.OperationStatus.Success);

        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Reset);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        var options = tester.Services.GetRequiredService<IOptions<InstanceOptions>>().Value;
        _fileSystem.ResetFileExists(options).Should().BeTrue();
        (await tester.Harness.Sent.Any<ShutdownInstance>()).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_error_event_on_reset_failure()
    {
        // Arrange
        _pipeClient.SetupResetSystemResult(CommunicationEnums.OperationStatus.Error, "ERROR");
        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Reset);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemError>()).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.ResetSystem, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_restart_system_to_host_management()
    {
        // Arrange
        _pipeClient.SetupRestartSystemResult(CommunicationEnums.OperationStatus.Success);

        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Restart);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemCompleted>()).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.RestartSystem, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_publish_error_event_on_restart_failure()
    {
        // Arrange
        _pipeClient.SetupRestartSystemResult(CommunicationEnums.OperationStatus.Error, "ERROR");
        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Restart);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemError>()).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.RestartSystem, string.Empty, Arg.Any<CancellationToken>());
    }
}
