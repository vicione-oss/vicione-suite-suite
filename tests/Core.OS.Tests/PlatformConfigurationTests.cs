using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.Module.Options;
using Core.OS.Extensions;
using Core.OS.Hosting;
using Core.OS.HostManagement;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Instance.Services;
using Core.OS.Logging;
using Core.OS.Modules;
using Core.OS.Modules.Hosting;
using Core.OS.Persistence;
using Core.OS.Tests.Extensions;
using Core.Shared.HostManagement;
using Core.Shared.Logging;
using Core.Shared.UserManagement.Configuration;
using Core.Tests.Tools;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.IO;
using Sdk.Backend.Messaging;
using Sdk.Backend.Modules;
using Sdk.Backend.Persistence;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.Testing.Backend;
using CallbackHandlerRegistry = HostManagement.Shared.Communication.NamedPipe.Client.CallbackHandlerRegistry;

namespace Core.OS.Tests;

public class PlatformConfigurationTests
{
    private static SuitePreparationContext GetSuitePreparationContext(IFileSystem? fileSystem, InstanceOptions instanceOptions)
        => new(
        fileSystem ?? Substitute.For<IFileSystem>(),
        instanceOptions,
        Substitute.For<ILoggerFactory>());

    private static ModulePreparationContext GetModulePreparationContext(IFileSystem? fileSystem, InstanceOptions instanceOptions)
        => new(
            Substitute.For<IHostApplicationBuilder>(),
        fileSystem ?? Substitute.For<IFileSystem>(),
        instanceOptions,
        Substitute.For<IArtifactRepositoryStore>(),
        Substitute.For<ILoggerFactory>())
        {
            ModuleHost = Substitute.For<IModuleHost>(),
            ModuleOptionsStore = Substitute.For<IModuleOptionsStore>(),
            RepositoryOptionsCache = Substitute.For<IArtifactRepositoryOptionsCache>()
        };

    public class AddCoreOs : PlatformConfigurationTests
    {
        [Fact]
        public async Task Should_configure_platform_services()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            using var config = new ConfigurationManager();
            config.AddConfiguration(new TestConfig()
                .SetSetting("Instance:BackupDirectory", "Backup")
                .ConfigureModuleLoader()
                .AddTestUiHost()
                .AddTestBackendClientModule()
                .BuildConfiguration());

            var instanceOptions = config.GetInstanceOptions();
            var context = GetSuitePreparationContext(fileSystem, instanceOptions);
            context.ModuleContext = GetModulePreparationContext(fileSystem, instanceOptions);

            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddCoreOs(config, context);

            fileSystem.EnsureInstanceIdFile(config.GetInstanceOptions());

            var messageBannerService = Substitute.For<IMessageBannerService>();
            services.AddSingleton(messageBannerService);

            // Act
            await using var serviceProvider = services.BuildServiceProvider();

            // Assert
            Assert.NotNull(serviceProvider.GetService<IOptions<InstanceOptions>>());
            Assert.NotNull(serviceProvider.GetService<IOptions<ModuleLoaderOptions>>());
            Assert.NotNull(serviceProvider.GetService<IOptions<InstanceOptions>>());
            Assert.NotNull(serviceProvider.GetService<IOptions<LoggingOptions>>());
            Assert.NotNull(serviceProvider.GetService<IOptions<HealthCheckPublisherOptions>>());
            Assert.NotNull(serviceProvider.GetService<IOptions<HostManagementOptions>>());
            Assert.NotNull(serviceProvider.GetService<IOptions<ExternalIdProviderOptions>>());
            Assert.NotNull(serviceProvider.GetService<ILogOptions>());

            Assert.NotNull(serviceProvider.GetService<ISuiteMediator>());
            Assert.NotNull(serviceProvider.GetService<ILocalInstanceInformationProvider>());
            Assert.NotNull(serviceProvider.GetService<IInstanceInformationProvider>());
            Assert.NotNull(serviceProvider.GetService<IModuleHost>());
            Assert.NotNull(serviceProvider.GetService<SynchronizationState>());
            Assert.NotNull(serviceProvider.GetService<IPipeClient>());
            Assert.NotNull(serviceProvider.GetService<CallbackHandlerRegistry>());
            Assert.Null(serviceProvider.GetService<IHealthCheckPublisher>());

            Assert.NotNull(serviceProvider.GetService<IFileSystem>());
            Assert.NotNull(serviceProvider.GetService<IAtomicFileWriter>());
        }
    }

    public class AddCoreServices : PlatformConfigurationTests
    {
        [Fact]
        public async Task Should_configure_master_services_when_instance_is_master()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var moduleHost = Substitute.For<IModuleHost>();

            using var config = new ConfigurationManager();
            config.AddConfiguration(new TestConfig()
                .UseInstanceType(InstanceType.Master)
                .SetSetting("Instance:BackupDirectory", "Backup")
                .SetSetting("ConnectionStrings:Postgres", "Totally valid ConnectionString")
                .UseInMemoryBus(false)
                .ConfigureModuleLoader()
                .AddTestUiHost()
                .AddTestBackendClientModule()
                .BuildConfiguration());

            fileSystem.EnsureInstanceIdFile(config.GetInstanceOptions());

            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddSingleton<IFileSystem>(fileSystem)
                .AddSingleton(moduleHost)
                .AddSingleton(Substitute.For<IWorkspaceProvider<SystemBackendModule>>())
                .AddCoreServices(config, moduleHost);

            var instanceInfoProviderOverride = Substitute.For<ILocalInstanceInformationProvider>();
            var messageBannerService = Substitute.For<IMessageBannerService>();
            instanceInfoProviderOverride.Local.Returns(Substitute.For<IInstanceInformation>());
            services.AddSingleton(instanceInfoProviderOverride);
            services.AddSingleton(messageBannerService);

            // Act
            await using var serviceProvider = services.BuildServiceProvider();

            // Assert
            Assert.NotNull(serviceProvider.GetService<IMasterDbConnectionStringProvider>());
            Assert.NotNull(serviceProvider.GetService<IInstanceConfigurationRepository>());
            Assert.NotNull(serviceProvider.GetService<ISaveChangesInterceptor>());
            Assert.NotNull(serviceProvider.GetService<IHealthCheckPublisher>());
        }

        [Fact]
        public async Task Should_not_configure_health_check_publisher_for_in_memory_bus()
        {
            // Arrange
            var moduleHost = Substitute.For<IModuleHost>();

            using var config = new ConfigurationManager();
            config.AddConfiguration(new TestConfig()
                .UseInstanceType(InstanceType.Master)
                .SetSetting("Instance:BackupDirectory", "Backup")
                .SetSetting("ConnectionStrings:Postgres", "Totally valid ConnectionString")
                .ConfigureModuleLoader()
                .AddTestUiHost()
                .AddTestBackendClientModule()
                .UseInMemoryBus()
                .BuildConfiguration());

            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IConfiguration>(config)
                .AddCoreServices(config, moduleHost);

            // Act
            await using var serviceProvider = services.BuildServiceProvider();

            // Assert
            Assert.Null(serviceProvider.GetService<IHealthCheckPublisher>());
        }
    }
}
