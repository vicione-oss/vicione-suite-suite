using System.IO.Abstractions.TestingHelpers;
using Core.Module.Options;
using Core.OS.Extensions;
using Core.OS.HostManagement;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Logging;
using Core.OS.Modules;
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
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Backend.Modules;
using Sdk.Backend.Persistence;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests;

public class PlatformConfigurationTests
{
    [Fact]
    public async Task Should_configure_platform_services()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var moduleHost = Substitute.For<IModuleHost>();

        using var config = new ConfigurationManager();
        config.AddConfiguration(new TestConfig()
            .SetSetting("Instance:BackupDirectory", "Backup")
            .ConfigureModuleLoader()
            .AddTestUiHost()
            .AddTestBackendClientModule()
            .BuildConfiguration());

        var instanceOptions = config.GetInstanceOptions();

        var services = new ServiceCollection()
            .ConfigureAndValidateOptions(instanceOptions)
            .AddLogging()
            .AddSingleton<IConfiguration>(config)
            .AddSingleton(Substitute.For<IModuleArtifactCache>())
            .AddSingleton(Substitute.For<IModuleMetadataProvider>())
            .AddSingleton(moduleHost)
            .AddCoreServices(fileSystem, config, moduleHost);

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
        Assert.NotNull(serviceProvider.GetService<EventCallbackRegistry>());
        Assert.Null(serviceProvider.GetService<IHealthCheckPublisher>());
    }

    [Fact]
    public async Task As_master_should_configure_master_services()
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

        var instanceOptions = config.GetInstanceOptions();
        fileSystem.EnsureInstanceIdFile(config.GetInstanceOptions());

        var services = new ServiceCollection()
            .ConfigureAndValidateOptions(instanceOptions)
            .AddLogging()
            .AddSingleton<IConfiguration>(config)
            .AddSingleton(moduleHost)
            .AddSingleton(Substitute.For<IWorkspaceProvider<SystemBackendModule>>())
            .AddCoreServices(fileSystem, config, moduleHost);

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
    public async Task Health_check_should_not_be_configured_for_memory_bus()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
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
            .AddSingleton(moduleHost)
            .AddCoreServices(fileSystem, config, moduleHost);

        // Act
        await using var serviceProvider = services.BuildServiceProvider();

        // Assert
        Assert.Null(serviceProvider.GetService<IHealthCheckPublisher>());
    }
}
