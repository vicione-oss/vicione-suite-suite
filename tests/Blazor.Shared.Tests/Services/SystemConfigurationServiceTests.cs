using AwesomeAssertions;
using Blazor.Shared.Services;
using Core.Shared.HostManagement;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Xunit;

namespace Blazor.Shared.Tests.Services;

public class SystemConfigurationServiceTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ILogger<SystemConfigurationService> _logger = Substitute.For<ILogger<SystemConfigurationService>>();

    private SystemConfigurationService CreateService() => new(_mediator, _logger);

    public sealed class SystemConfiguration_Property : SystemConfigurationServiceTests
    {
        [Fact]
        public void Should_throw_before_initialize()
        {
            // Arrange
            using var service = CreateService();

            // Act
            var act = () => service.SystemConfiguration;

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_return_configuration_after_initialize()
        {
            // Arrange
            using var service = CreateService();
            var configuration = new HostManagement.Shared.Contracts.SystemConfiguration();

            _mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(new GetHostMgmtSystemConfigurationResponse { Configuration = configuration });

            // Act
            await service.Initialize(TestContext.Current.CancellationToken);

            // Assert
            service.SystemConfiguration.Should().NotBeNull();
        }
    }

    public sealed class Initialize : SystemConfigurationServiceTests
    {
        [Fact]
        public async Task Should_set_last_dhcp_lease_fetch_utc()
        {
            // Arrange
            using var service = CreateService();
            var configuration = new HostManagement.Shared.Contracts.SystemConfiguration();

            _mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(new GetHostMgmtSystemConfigurationResponse { Configuration = configuration });

            // Act
            await service.Initialize(TestContext.Current.CancellationToken);

            // Assert
            service.LastDhcpLeaseFetchUtc.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_throw_when_response_has_error()
        {
            // Arrange
            using var service = CreateService();

            _mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(new GetHostMgmtSystemConfigurationResponse { RequestError = new ErrorInfo(1, "error") });

            // Act
            var act = () => service.Initialize(TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_throw_when_configuration_is_null()
        {
            // Arrange
            using var service = CreateService();

            _mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(new GetHostMgmtSystemConfigurationResponse { Configuration = null });

            // Act
            var act = () => service.Initialize(TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_not_throw_when_cancelled()
        {
            // Arrange
            using var service = CreateService();

            _mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var act = () => service.Initialize(TestContext.Current.CancellationToken);

            // Assert
            await act.Should().NotThrowAsync();
        }
    }

    public sealed class SetSystemConfiguration : SystemConfigurationServiceTests
    {
        [Fact]
        public async Task Should_raise_system_configuration_changed_event()
        {
            // Arrange
            using var service = CreateService();
            var configuration = new HostManagement.Shared.Contracts.SystemConfiguration();
            var eventRaised = false;
            service.SystemConfigurationChanged += (token) =>
            {
                eventRaised = true;
                return Task.CompletedTask;
            };

            // Act
            await service.SetSystemConfiguration(configuration, TestContext.Current.CancellationToken);

            // Assert
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public async Task Should_update_last_dhcp_lease_fetch_utc()
        {
            // Arrange
            using var service = CreateService();
            var configuration = new HostManagement.Shared.Contracts.SystemConfiguration();

            // Act
            await service.SetSystemConfiguration(configuration, TestContext.Current.CancellationToken);

            // Assert
            service.LastDhcpLeaseFetchUtc.Should().NotBeNull();
        }
    }

    public sealed class Consume : SystemConfigurationServiceTests
    {
        [Fact]
        public async Task Should_reinitialize_on_system_configuration_changed_event()
        {
            // Arrange
            using var service = CreateService();
            var configuration = new HostManagement.Shared.Contracts.SystemConfiguration();

            _mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(
                Arg.Any<GetHostMgmtSystemConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(new GetHostMgmtSystemConfigurationResponse { Configuration = configuration });

            var context = new ClientContext<Sdk.SystemConfiguration.Events.SystemConfigurationChanged>(
                new Sdk.SystemConfiguration.Events.SystemConfigurationChanged { CorrelationId = Guid.NewGuid() }, null);

            // Act
            await service.Consume(context, CancellationToken.None);

            // Assert
            service.SystemConfiguration.Should().NotBeNull();
        }
    }
}
