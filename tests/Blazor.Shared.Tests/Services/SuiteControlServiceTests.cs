using AwesomeAssertions;
using Blazor.Shared.Services;
using Core.Shared.HostManagement.Commands;
using Core.Shared.Instance.Commands;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Instance;
using Xunit;

namespace Blazor.Shared.Tests.Services;

public class SuiteControlServiceTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IInstanceInformationProvider _informationProvider = Substitute.For<IInstanceInformationProvider>();
    private readonly IMessageBannerService _bannerService = Substitute.For<IMessageBannerService>();
    private readonly ILogger<SuiteControlService> _logger = Substitute.For<ILogger<SuiteControlService>>();

    private SuiteControlService CreateService(Guid? instanceId = null)
    {
        var instanceInfo = Substitute.For<IInstanceInformation>();
        instanceInfo.Id.Returns(instanceId ?? Guid.NewGuid());
        _informationProvider.Local.Returns(instanceInfo);

        return new SuiteControlService(_mediator, _informationProvider, _bannerService, _logger);
    }

    public sealed class RestartSuite : SuiteControlServiceTests
    {
        [Fact]
        public async Task Should_show_message_banner()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.RestartSuite();

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
            await service.RestartSuite();

            // Assert
            await _mediator.Received(1).Send(
                Arg.Is<ControlInstance>(cmd =>
                    cmd.InstanceId == instanceId &&
                    cmd.Action == InstanceCommand.Restart &&
                    cmd.Delay > TimeSpan.Zero &&
                    cmd.CorrelationId != Guid.Empty),
                instanceId);
        }

        [Fact]
        public async Task Should_generate_unique_correlation_id()
        {
            // Arrange
            var service = CreateService();
            var capturedCorrelationIds = new List<Guid>();

            await _mediator.Send(Arg.Do<ControlInstance>(cmd => capturedCorrelationIds.Add(cmd.CorrelationId)), Arg.Any<Guid>());

            // Act
            await service.RestartSuite();
            await service.RestartSuite();

            // Assert
            capturedCorrelationIds.Should().HaveCount(2);
            capturedCorrelationIds[0].Should().NotBe(Guid.Empty);
            capturedCorrelationIds[1].Should().NotBe(Guid.Empty);
            capturedCorrelationIds[0].Should().NotBe(capturedCorrelationIds[1]);
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
                Arg.Is<ControlSystem>(cmd => cmd.Command == SystemCommand.Restart),
                instanceId);
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
                Arg.Is<ControlSystem>(cmd => cmd.Command == SystemCommand.Shutdown),
                instanceId);
        }
    }
}
