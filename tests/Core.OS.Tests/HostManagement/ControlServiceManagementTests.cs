using System.IO.Abstractions.TestingHelpers;
using Core.OS.HostManagement;
using Core.OS.Instance;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Contracts.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.SystemConfiguration.Commands;
using Sdk.SystemConfiguration.Contracts;
using Sdk.SystemConfiguration.Events;
using HostServiceState = HostManagement.Shared.Enums.ServiceState;

namespace Core.OS.Tests.HostManagement;

public sealed class ControlServiceManagementTests
{
    private const string ServiceName = "ControlServiceTest";
    private const string HostManagementRejection = "Topic cannot be used because its capability is disabled.";

    private static ServiceProvider CreateServices()
    {
        return new ServiceCollection()
            .AddSingleton<ControlServiceManagement>()
            .AddSingleton(Substitute.For<ILogger<ControlServiceManagement>>())
            .AddSingleton<SystemConfigurationCache>()
            .AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>())
            .AddMockPipeClientSystemConfiguration()
            .BuildServiceProvider();
    }

    [Fact]
    public async Task Should_be_available()
    {
        // Arrange + Act
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Assert
        service.IsAvailable.Should().BeTrue();
    }
    
    [Fact]
    public async Task Should_not_be_available_when_host_management_is_mocked()
    {
        // Arrange + Act
        await using var services = CreateServices(_ => CreateMockPipeClient());
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Assert
        service.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Should_fail_as_unavailable_when_the_pipe_never_connected()
    {
        // Arrange
        await using var services = CreateServices(_ => new UnreachablePipeClient());
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(ServiceCommand.Stop, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error!.ErrorCode.Should().Be(ControlServiceErrorCodes.ControlServiceUnavailable);
    }

    [Fact]
    public async Task Should_enable_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(ServiceCommand.Enable, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Enabled);
    }

    [Fact]
    public async Task Should_disable_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(ServiceCommand.Disable, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Disabled);
    }

    [Fact]
    public async Task Should_restart_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(ServiceCommand.Restart, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Enabled);
    }

    [Fact]
    public async Task Should_start_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(ServiceCommand.Start, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Enabled);
    }

    [Fact]
    public async Task Should_stop_unknown_service()
    {
        // Arrange
        await using var services = CreateServices();
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(ServiceCommand.Stop, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ServiceName.Should().Be(ServiceName);
        result.State.Should().Be(ServiceState.Disabled);
    }

    [Fact]
    public async Task Should_restart_through_the_restart_service_topic()
    {
        // Arrange
        await using var pipeClient = CreateAvailablePipeClient();
        pipeClient.SetupServiceControlResult(Topics.RestartService, OperationStatus.Success);
        await using var services = CreateServices(_ => pipeClient);
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(ServiceCommand.Restart, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        await pipeClient.Received(1).SendRequest(Topics.RestartService, ServiceName, Arg.Any<CancellationToken>());
        await pipeClient.DidNotReceive().SendRequest(Topics.StopService, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await pipeClient.DidNotReceive().SendRequest(Topics.StartService, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ServiceCommand.Start, Topics.StartService)]
    [InlineData(ServiceCommand.Stop, Topics.StopService)]
    [InlineData(ServiceCommand.Restart, Topics.RestartService)]
    public async Task Should_report_an_unknown_state_when_host_management_rejects_a_state_change(ServiceCommand command, string topic)
    {
        // Arrange
        await using var pipeClient = CreateAvailablePipeClient();
        pipeClient.SetupServiceControlResult(topic, OperationStatus.Error, HostManagementRejection);
        await using var services = CreateServices(_ => pipeClient);
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(command, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.State.Should().Be(ServiceState.Unknown);
        result.Error!.Message.Should().Contain(HostManagementRejection);
    }

    [Theory]
    [InlineData(ServiceCommand.Enable, HostServiceState.Disabled)]
    [InlineData(ServiceCommand.Disable, HostServiceState.Enabled)]
    [InlineData(ServiceCommand.Enable, null)]
    public async Task Should_report_an_unknown_state_and_keep_the_cache_when_host_management_rejects_a_configuration_change(
        ServiceCommand command, HostServiceState? currentState)
    {
        // Arrange
        await using var pipeClient = CreateAvailablePipeClient();
        pipeClient.SetupGetSystemConfigurationResult(OperationStatus.Success, CreateConfiguration(currentState));
        pipeClient.SetupSetSystemConfigurationResult(OperationStatus.Error, HostManagementRejection);
        await using var services = CreateServices(_ => pipeClient);
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(command, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.State.Should().Be(ServiceState.Unknown);
        result.Error!.Message.Should().Contain(HostManagementRejection);
        services.GetRequiredService<SystemConfigurationCache>().Get().Should().Be(CreateConfiguration(currentState));
    }

    [Theory]
    [InlineData(ServiceCommand.Enable)]
    [InlineData(ServiceCommand.Disable)]
    public async Task Should_invalidate_the_cache_when_host_management_accepts_a_configuration_change(ServiceCommand command)
    {
        // Arrange
        await using var pipeClient = CreateAvailablePipeClient();
        pipeClient.SetupGetSystemConfigurationResult(OperationStatus.Success, CreateConfiguration(HostServiceState.Unknown));
        pipeClient.SetupSetSystemConfigurationResult(OperationStatus.Success);
        await using var services = CreateServices(_ => pipeClient);
        var service = services.GetRequiredService<ControlServiceManagement>();

        // Act
        var result = await service.TryControlService(command, ServiceName, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        services.GetRequiredService<SystemConfigurationCache>().Get().Should().BeNull();
    }

    private static IPipeClient CreateAvailablePipeClient()
    {
        var pipeClient = Substitute.For<IPipeClient>();
        pipeClient.State.Returns(PipeState.Connected);

        return pipeClient;
    }

    private static global::HostManagement.Shared.Contracts.SystemConfiguration CreateConfiguration(HostServiceState? serviceState)
    {
        var configuration = TestPipeClient.GetEmbeddedSystemConfiguration();
        if (serviceState is not null)
            configuration.Services.Add(new ServiceDetail { Name = ServiceName, State = serviceState.Value });

        return configuration;
    }

    private static ServiceProvider CreateServices(Func<IServiceProvider, IPipeClient> pipeClientFactory)
    {
        return new ServiceCollection()
            .AddSingleton<ControlServiceManagement>()
            .AddSingleton(Substitute.For<ILogger<ControlServiceManagement>>())
            .AddSingleton<SystemConfigurationCache>()
            .AddSingleton(Substitute.For<ILogger<SystemConfigurationCache>>())
            .AddSingleton(pipeClientFactory)
            .AddSingleton(Options.Create(new HostManagementOptions()))
            .BuildServiceProvider();
    }

    private static MockPipeClient CreateMockPipeClient()
    {
        var fileSystem = new MockFileSystem();
        var instanceOptions = new InstanceOptions
        {
            HomeDirectory = fileSystem.Path.GetFullPath("AppData"),
            CacheDirectory = fileSystem.Path.GetFullPath("Cache"),
            BackupDirectory = fileSystem.Path.GetFullPath("Backup"),
            Type = Sdk.Instance.InstanceType.Standalone,
        };
        var mockOptions = new MockPipeClientOptions
        {
            Enabled = true,
            DataSource = MockPipeClientDataSource.SystemConfigurationEmbedded,
        };

        return new MockPipeClient(fileSystem,
            Substitute.For<Microsoft.Extensions.Hosting.IHostApplicationLifetime>(),
            Options.Create(mockOptions),
            Options.Create(instanceOptions));
    }

    private sealed class UnreachablePipeClient : IPipeClient
    {
        public PipeState State => PipeState.NotOpened;

        public bool IsMock => false;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task Connect(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Failed to connect to pipe 'test'.");

        public Task<string> SendRequest(string topic, string content, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Failed to connect to pipe 'test'.");
    }
}
