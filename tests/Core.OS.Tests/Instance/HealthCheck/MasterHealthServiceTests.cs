using Core.OS.Instance;
using Core.OS.Instance.HealthCheck;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.HealthCheck;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.HealthCheck;

public class MasterHealthServiceTests
{
    private readonly IInstanceInformationProvider _instanceProvider = Substitute.For<IInstanceInformationProvider>();
    private readonly ISuiteMediator _mediator = Substitute.For<ISuiteMediator>();
    private readonly IOptions<InstanceOptions> _options = Substitute.For<IOptions<InstanceOptions>>();
    private readonly ILogger<MasterHealthService> _logger = Substitute.For<ILogger<MasterHealthService>>();

    public class CheckHealthStatus : MasterHealthServiceTests
    {
        [Fact]
        public async Task Sets_correct_status_when_health_check_received()
        {
            // Arrange
            var masterId = Guid.NewGuid();

            _instanceProvider.Local.Returns(new InstanceInformation()
            {
                Id = masterId,
                Type = InstanceType.Master
            });

            _instanceProvider
                .GetInstancesInCluster(TestContext.Current.CancellationToken)
                .Returns(
                [
                    new TestInstanceInformation { Id = masterId, Type = InstanceType.Master },
                ]);

            _options.Value.Returns(new InstanceOptions
            {
                HomeDirectory = "AppData",
                CacheDirectory = "Cache",
                BackupDirectory = "Backup",
                Type = InstanceType.Slave,
                HealthChecks = new()
                {
                    MasterPublishIntervalInSeconds = -4,
                },
            });

            await using var services = new ServiceCollection()
                .AddSingleton(_instanceProvider)
                .AddSingleton(_mediator)
                .BuildServiceProvider();

            var info = new MasterHealthInfo();
            using var service = new MasterHealthService(info, services, _options, _logger);

            // Act
            await service.CheckHealthStatus(masterId, HealthStatus.Healthy, TestContext.Current.CancellationToken);

            // Assert
            info.IsMasterReachable.Should().BeTrue();
        }

        [Fact]
        public async Task Sets_correct_status_when_health_check_not_received()
        {
            // Arrange
            var info = new MasterHealthInfo();

            _instanceProvider.Local.Returns(new InstanceInformation
            {
                Id = Guid.NewGuid(),
                Type = InstanceType.Slave
            });

            _options.Value.Returns(new InstanceOptions
            {
                HomeDirectory = "AppData",
                CacheDirectory = "Cache",
                BackupDirectory = "Backup",
                Type = InstanceType.Master,
                HealthChecks = new()
                {
                    MasterPublishIntervalInSeconds = -4,
                },
            });

            _instanceProvider
                .GetInstancesInCluster(Arg.Any<CancellationToken>())
                .Returns(
                [
                    new TestInstanceInformation { Id = Guid.NewGuid(), Type = InstanceType.Master },
                ]);

            await using var services = new ServiceCollection()
                .AddSingleton(_instanceProvider)
                .AddSingleton(_mediator)
                .BuildServiceProvider();

            using var service = new MasterHealthService(info, services, _options, _logger);

            // Act
            await service.CheckHealthStatus(Guid.NewGuid(), HealthStatus.Healthy, TestContext.Current.CancellationToken);

            // Assert
            info.IsMasterReachable.Should().BeFalse();
        }
    }
}
