using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.HealthCheck;
using Core.OS.Persistence;
using Core.Shared.Instance.HealthCheck;
using Core.Shared.Instance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Xunit;

namespace Core.OS.Tests.Instance.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddInstanceServices
    {
        [Fact]
        public void Should_register_master_services_when_instance_type_is_master()
        {
            // Arrange
            var services = new ServiceCollection();
            var options = new InstanceOptions
            {
                Type = InstanceType.Master,
                HomeDirectory = "AppData",
                CacheDirectory = "Cache",
                BackupDirectory = "Backup",
            };

            // Act
            services.AddInstanceServices(options, useInMemoryBus: false);

            // Assert
            services.Should().Contain(s => s.ServiceType == typeof(IMasterDbConnectionStringProvider));
            services.Should().Contain(s => s.ServiceType == typeof(IInstanceConfigurationRepository));
            services.Should().Contain(s => s.ServiceType == typeof(ISaveChangesInterceptor));
        }

        [Fact]
        public void Should_register_healthcheck_and_masterhealth_services_when_master_and_not_inmemory()
        {
            // Arrange
            var services = new ServiceCollection();
            var options = new InstanceOptions
            {
                Type = InstanceType.Master,
                HomeDirectory = "AppData",
                CacheDirectory = "Cache",
                BackupDirectory = "Backup",
            };

            // Act
            services.AddInstanceServices(options, useInMemoryBus: false);

            // Assert
            services.Should().Contain(s => s.ServiceType == typeof(IHealthCheckPublisher)
                                           && s.ImplementationType == typeof(InstanceHealthCheckPublisher));
            services.Should().Contain(s => s.ServiceType == typeof(IMasterHealthService)
                                           && s.ImplementationType == typeof(MasterHealthService));
            services.Should().Contain(s => s.ServiceType == typeof(IMasterHealthInfo));
        }

        [Fact]
        public void Should_register_mock_masterhealth_when_standalone()
        {
            // Arrange
            var services = new ServiceCollection();
            var options = new InstanceOptions
            {
                Type = InstanceType.Standalone,
                HomeDirectory = "AppData",
                CacheDirectory = "Cache",
                BackupDirectory = "Backup",
            };

            // Act
            services.AddInstanceServices(options, useInMemoryBus: true);

            // Assert
            services.Should().Contain(s => s.ImplementationType == typeof(MockMasterHealthService));
        }

        [Fact]
        public void Should_register_general_instance_services_always()
        {
            // Arrange
            var services = new ServiceCollection();
            var options = new InstanceOptions
            {
                Type = InstanceType.Slave,
                HomeDirectory = "AppData",
                CacheDirectory = "Cache",
                BackupDirectory = "Backup",
            };

            // Act
            services.AddInstanceServices(options, useInMemoryBus: true);

            // Assert
            services.Should().Contain(s => s.ServiceType == typeof(ILocalInstanceInformationProvider));
            services.Should().Contain(s => s.ServiceType == typeof(IInstanceInformationProvider));
            services.Should().Contain(s => s.ServiceType == typeof(IClusterInformationProvider));
            services.Should().Contain(s => s.ServiceType == typeof(ITicketStore));
            services.Should().Contain(s => s.ServiceType == typeof(IBackupStore));
            services.Should().Contain(s => s.ServiceType == typeof(IBackupFactory));
            services.Should().Contain(s => s.ServiceType == typeof(INonceStore));
            services.Should().Contain(s => s.ServiceType == typeof(IOnboardingStateStore));
            services.Should().Contain(s => s.ServiceType == typeof(IStreamUploadHandler));
        }
    }
}
