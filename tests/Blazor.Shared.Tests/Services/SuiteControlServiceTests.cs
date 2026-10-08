using Blazor.Shared.Services;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Capabilities;
using Core.Shared.Instance.Commands;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.Tests.Services;

public class SuiteControlServiceTests
{
    private static readonly SystemControlCapabilities AllEnabled = new(CapabilityStatus.Enabled, CapabilityStatus.Enabled, CapabilityStatus.Enabled);

    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IInstanceInformationProvider _informationProvider = Substitute.For<IInstanceInformationProvider>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();
    private readonly ILogger<SuiteControlService> _logger = Substitute.For<ILogger<SuiteControlService>>();

    private SuiteControlService CreateService(Guid? instanceId = null)
    {
        var instanceInfo = Substitute.For<IInstanceInformation>();
        instanceInfo.Id.Returns(instanceId ?? Guid.NewGuid());
        _informationProvider.Local.Returns(instanceInfo);
        SetupSystemControlCapabilities(AllEnabled);

        return new SuiteControlService(_mediator, _informationProvider, _bannerService, _logger);
    }

    private void SetupSystemControlCapabilities(SystemControlCapabilities? capabilities)
        => _mediator.Request<GetSystemControlCapabilities, GetSystemControlCapabilitiesResponse>(Arg.Any<GetSystemControlCapabilities>(), Arg.Any<CancellationToken>())
            .Returns(new GetSystemControlCapabilitiesResponse { Capabilities = capabilities });

    private void AssertFunctionDisabledBannerShownOnly()
    {
        _bannerService.Received(1).ShowMessageBanner(MessageType.Error, Localization.HostManagementCapabilities.FunctionDisabled);
        _bannerService.DidNotReceive().ShowMessageBanner(MessageType.Information, Arg.Any<string>());
    }

    public sealed class RestartSuite : SuiteControlServiceTests
    {
        [Fact]
        public async Task Should_show_message_banner()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.RestartInstance();

            // Assert
            _bannerService.Received(1).ShowMessageBanner(
                Sdk.MessageBanner.Contracts.MessageType.Information,
                Arg.Any<string>());
        }

        [Fact]
        public async Task Should_send_control_instance_command_with_restart_action()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var service = CreateService(instanceId);

            // Act
            await service.RestartInstance();

            // Assert
            await _mediator.Received(1).Send(
                Arg.Is<ControlInstance>(cmd =>
                    cmd!.InstanceId == instanceId &&
                    cmd.Action == InstanceCommand.Restart &&
                    cmd.Delay > TimeSpan.Zero &&
                    cmd.CorrelationId != Guid.Empty),
                instanceId,
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_generate_unique_correlation_id()
        {
            // Arrange
            var service = CreateService();
            var capturedCorrelationIds = new List<Guid>();

            await _mediator.Send(Arg.Do<ControlInstance>(cmd => capturedCorrelationIds.Add(cmd.CorrelationId)), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

            // Act
            await service.RestartInstance();
            await service.RestartInstance();

            // Assert
            capturedCorrelationIds.Should().HaveCount(2);
            capturedCorrelationIds[0].Should().NotBe(Guid.Empty);
            capturedCorrelationIds[1].Should().NotBe(Guid.Empty);
            capturedCorrelationIds[0].Should().NotBe(capturedCorrelationIds[1]);
        }

        [Fact]
        public async Task Should_show_error_and_send_nothing_when_restart_service_is_disabled()
        {
            // Arrange
            var service = CreateService();
            SetupSystemControlCapabilities(AllEnabled with { RestartService = CapabilityStatus.Disabled });

            // Act
            await service.RestartInstance();

            // Assert
            AssertFunctionDisabledBannerShownOnly();
            await _mediator.DidNotReceive().Send(Arg.Any<ControlInstance>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_restart_when_the_capabilities_are_unknown()
        {
            // Arrange
            var service = CreateService();
            SetupSystemControlCapabilities(null);

            // Act
            await service.RestartInstance();

            // Assert
            await _mediator.Received(1).Send(Arg.Any<ControlInstance>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }

    public sealed class RestartCluster : SuiteControlServiceTests
    {
        [Fact]
        public async Task Should_show_message_banner()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.RestartAllInstances();

            // Assert
            _bannerService.Received(1).ShowMessageBanner(
                Sdk.MessageBanner.Contracts.MessageType.Information,
                Arg.Any<string>());
        }

        [Fact]
        public async Task Should_send_restart_cluster_command_with_delay()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.RestartAllInstances();

            // Assert
            await _mediator.Received(1).Send(
                Arg.Is<Core.Shared.Instance.Commands.RestartAllInstances>(cmd =>
                    cmd!.Delay > TimeSpan.Zero &&
                    cmd.CorrelationId != Guid.Empty),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_show_error_and_send_nothing_when_restart_service_is_disabled()
        {
            // Arrange
            var service = CreateService();
            SetupSystemControlCapabilities(AllEnabled with { RestartService = CapabilityStatus.Disabled });

            // Act
            await service.RestartAllInstances();

            // Assert
            AssertFunctionDisabledBannerShownOnly();
            await _mediator.DidNotReceive().Send(Arg.Any<Core.Shared.Instance.Commands.RestartAllInstances>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_restart_when_the_capabilities_are_unknown()
        {
            // Arrange
            var service = CreateService();
            SetupSystemControlCapabilities(null);

            // Act
            await service.RestartAllInstances();

            // Assert
            await _mediator.Received(1).Send(Arg.Any<Core.Shared.Instance.Commands.RestartAllInstances>(), Arg.Any<CancellationToken>());
        }
    }

    public sealed class RestartSystem : SuiteControlServiceTests
    {
        [Fact]
        public async Task Should_send_control_system_command_with_restart_action()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var service = CreateService(instanceId);

            // Act
            await service.RestartSystem();

            // Assert
            await _mediator.Received(1).Send(
                Arg.Is<ControlSystem>(cmd => cmd!.Command == SystemCommand.Restart),
                instanceId,
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_show_error_and_send_nothing_when_restart_system_is_disabled()
        {
            // Arrange
            var service = CreateService();
            SetupSystemControlCapabilities(AllEnabled with { RestartSystem = CapabilityStatus.Disabled });

            // Act
            await service.RestartSystem();

            // Assert
            AssertFunctionDisabledBannerShownOnly();
            await _mediator.DidNotReceive().Send(Arg.Any<ControlSystem>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_restart_when_the_capabilities_are_unknown()
        {
            // Arrange
            var service = CreateService();
            SetupSystemControlCapabilities(null);

            // Act
            await service.RestartSystem();

            // Assert
            await _mediator.Received(1).Send(Arg.Any<ControlSystem>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }

    public sealed class ShutdownSystem : SuiteControlServiceTests
    {
        [Fact]
        public async Task Should_send_control_system_command_with_shutdown_action()
        {
            // Arrange
            var service = CreateService();
            var instanceId = _informationProvider.Local.Id;

            // Act
            await service.ShutdownSystem();

            // Assert
            await _mediator.Received(1).Send(
                Arg.Is<ControlSystem>(cmd => cmd!.Command == SystemCommand.Shutdown),
                instanceId,
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_show_error_and_send_nothing_when_shutdown_system_is_disabled()
        {
            // Arrange
            var service = CreateService();
            SetupSystemControlCapabilities(AllEnabled with { ShutdownSystem = CapabilityStatus.Disabled });

            // Act
            await service.ShutdownSystem();

            // Assert
            AssertFunctionDisabledBannerShownOnly();
            await _mediator.DidNotReceive().Send(Arg.Any<ControlSystem>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_shut_down_when_the_capabilities_are_unknown()
        {
            // Arrange
            var service = CreateService();
            SetupSystemControlCapabilities(null);

            // Act
            await service.ShutdownSystem();

            // Assert
            await _mediator.Received(1).Send(Arg.Any<ControlSystem>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
