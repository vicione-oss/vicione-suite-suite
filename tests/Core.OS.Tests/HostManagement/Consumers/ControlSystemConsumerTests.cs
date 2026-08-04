using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Tests.Extensions;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.UserManagement.Contracts;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Instance;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class ControlSystemConsumerTests
{
    private readonly IPipeClient _pipeClient = Substitute.For<IPipeClient>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly IQueryableUserStore<SuiteUser> _userStore = Substitute.For<IQueryableUserStore<SuiteUser>>();

    private readonly InstanceOptions _instanceOptions = new()
    {
        BackupDirectory = "backup",
        CacheDirectory = "cache",
        HomeDirectory = "home",
        Type = InstanceType.Standalone,
    };

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
            cfg.AddSingleton(Options.Create(options));
            cfg.SetupUserManager(_userStore);
        });

    [Fact]
    public async Task Should_send_reset_system_to_host_management()
    {
        // Arrange
        _pipeClient.SetupResetSystemResult(OperationStatus.Success);

        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Reset);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemCompleted>(TestContext.Current.CancellationToken)).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.ResetSystem, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_set_reset_flag_and_request_restart_by_host_management()
    {
        // Arrange
        _pipeClient.SetupResetSystemResult(OperationStatus.Success);

        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Reset);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        var options = tester.Services.GetRequiredService<IOptions<InstanceOptions>>().Value;
        _fileSystem.ResetFileExists(options).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.RestartService, _instanceOptions.ServiceName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_publish_error_event_on_reset_failure()
    {
        // Arrange
        _pipeClient.SetupResetSystemResult(OperationStatus.Error, "ERROR");
        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Reset);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemCompleted>(r =>
            r.Context.Message.ErrorInfo != null, TestContext.Current.CancellationToken)).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.ResetSystem, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_restart_system_to_host_management()
    {
        // Arrange
        _pipeClient.SetupRestartSystemResult(OperationStatus.Success);

        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Restart);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemCompleted>(TestContext.Current.CancellationToken)).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.RestartSystem, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_publish_error_event_on_restart_failure()
    {
        // Arrange
        _pipeClient.SetupRestartSystemResult(OperationStatus.Error, "ERROR");
        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Restart);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemCompleted>(r => r.Context.Message.ErrorInfo != null,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        await _pipeClient.Received().SendRequest(Topics.RestartSystem, string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_shutdown_system_request_to_host_management()
    {
        // Arrange        
        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Shutdown);
        SetupShutdownSystemRequestSuccess();

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        await _pipeClient.Received().SendRequest(Topics.ShutdownSystem,
                string.Empty,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_send_shutdown_system_started_event_on_success()
    {
        // Arrange        
        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Shutdown);
        SetupShutdownSystemRequestSuccess();

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemCompleted>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_send_control_instance_error_event_on_failure()
    {
        // Arrange
        await using var tester = SetupMassTransitTester();
        var command = new ControlSystem(SystemCommand.Shutdown);

        // Act
        await tester.TestInstanceDependentCommand<ControlSystem, ControlSystemConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ControlSystemCompleted>(r =>
            r.Context.Message.ErrorInfo != null, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    private void SetupShutdownSystemRequestSuccess()
    {
        var result = new SystemControlResult
        {
            Status = OperationStatus.Success,
            Message = "Message"
        };

        _pipeClient.SendRequest(Topics.ShutdownSystem,
                string.Empty,
                Arg.Any<CancellationToken>()).Returns(JsonSerializer.Serialize(result, SourceGenerationContext.Default.SystemControlResult));

    }
}
