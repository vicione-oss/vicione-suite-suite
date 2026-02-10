using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Core.Module;
using Core.Module.Contracts;
using Core.OS.Instance;
using Core.OS.Logging;
using Core.OS.Modules;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using Core.UiHosting;
using DevExpress.Utils;
using AwesomeAssertions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Backend.Modules;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestModule.Backend.Controllers;
using TestUiHost;
using TestUiHost.Controllers;
using Xunit;
using Core.OS.Instance.Extensions;
using Core.OS.Tests.Extensions;
using Core.Shared.Modules.Contracts;
using Core.Tests.Tools;

namespace Core.OS.Tests.Modules.Services;

public class ModuleHostTests
{
    [Fact]
    public void Module_should_be_ordered_by_dependencies()
    {
        // Arrange
        var manager = CreateModuleHost();

        // Act
        var moduleInfos = manager.Modules.ToArray();

        // Assert
        moduleInfos.Should().HaveCount(4, "System, UiHost + TestBackend|Client");
    }

    public class AddModuleServices : ModuleHostTests
    {
        [Fact]
        public async Task Should_register_module_controllers_as_service()
        {
            // Arrange
            var testHost = new TestUiHostBackend();
            var testModule = new TestBackendModule();
            var manager = CreateModuleHostWithUiSupport(
                [
                    testHost, testModule,
                ],
                out var services,
                out _);

            var mvcBuilder = CreateMvcBuilder(services);

            // Act
            manager.AddUiHostServices(services, mvcBuilder, (_) => null);
            manager.AddModuleServices(services, mvcBuilder);

            // Assert
            await using var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetRequiredService<TestUiHostController>()
                .Should().NotBeNull();

            serviceProvider.GetRequiredService<TestController>()
                .Should()
                .NotBeNull();
        }

        [Fact]
        public async Task Should_add_workspace_service_per_module()
        {
            // Arrange
            var testHost = new TestUiHostBackend();
            var testModule = new TestBackendModule();
            var manager = CreateModuleHostWithUiSupport(
                [
                    testModule,
                    testHost
                ],
                out var services,
                out _);

            var mvcBuilder = CreateMvcBuilder(services);

            // Act
            manager.AddModuleServices(services, mvcBuilder);

            // Assert
            await using var serviceProvider = services.BuildServiceProvider();
            var wsProvider = serviceProvider.GetRequiredService<IWorkspaceProvider<TestBackendModule>>();
            wsProvider.Home.Should().NotBeNull();
            wsProvider.Cache.Should().NotBeNull();

            serviceProvider.GetService<IWorkspaceProvider<TestUiHostBackend>>()
                .Should()
                .BeNull();
        }

        [Fact]
        public void Should_configure_module_services()
        {
            // Arrange
            var module = new TestBackendModule();
            var manager = CreateModuleHostWithUiSupport(module, out var services, out _);
            var mvcBuilder = CreateMvcBuilder(services);

            // Act + Assert
            module.CallReceived += (_, s) => s.Should().Be(nameof(BackendModule.ConfigureServices));
            manager.AddModuleServices(services, mvcBuilder);
        }
    }

    public class AddUiHostServices : ModuleHostTests
    {
        [Fact]
        public async Task Should_init_ui_host_services()
        {
            // Arrange
            var testHost = new TestUiHostBackend();
            var manager = CreateModuleHostWithUiSupport(testHost, out var services, out _);
            var mvcBuilder = CreateMvcBuilder(services);
            services.AddModuleServices();

            var stack = new Stack<string>();
            stack.Push(nameof(IUiHostModule.ConfigureUiServices));
            stack.Push(nameof(BackendModule.ConfigureServices));
            stack.Push(nameof(IUiHostModule.LoadUiDependencies));

            // Act + Assert
            testHost.CallReceived += (_, s) => stack.Pop().Should().Be(s);
            manager.AddUiHostServices(services, mvcBuilder, (_) => null);

            await using var serviceProvider = services.BuildServiceProvider();
            var wsProvider = serviceProvider.GetRequiredService<IWorkspaceProvider<TestUiHostBackend>>();
            wsProvider.Home.Should().NotBeNull();
            wsProvider.Cache.Should().NotBeNull();
        }

        [Fact]
        public void Should_add_module_application_part()
        {
            // Arrange
            var testHost = new TestUiHostBackend();
            var manager = CreateModuleHostWithUiSupport(
                [
                    testHost,
                ],
                out var services,
                out _);

            var mvcBuilder = CreateMvcBuilder(services);

            // Act
            manager.AddUiHostServices(services, mvcBuilder, (_) => null);

            // Assert
            mvcBuilder.PartManager.ApplicationParts
                .Should()
                .ContainSingle(k => k.Name == testHost.GetType().Assembly.GetName().Name);
        }
    }

    public class GetModule : ModuleHostTests
    {
        [Fact]
        public void Should_get_existing_modules_by_type()
        {
            // Arrange
            var testHost = new TestUiHostBackend();
            var testModule = Substitute.For<TestBackendModule>();
            var manager = CreateModuleHostWithUiSupport(
                [testHost, testModule],
                out _,
                out _);

            // Act + Assert
            manager.GetModule<TestUiHostBackend>().Should().NotBeNull();
            manager.GetModule<TestBackendModule>().Should().NotBeNull();
        }
    }

    public class GetModuleAssemblies : ModuleHostTests
    {
        [Fact]
        public void Should_get_existing_module_assemblies()
        {
            // Arrange
            var testHost = new TestUiHostBackend();
            var testModule = Substitute.For<TestBackendModule>();
            var manager = CreateModuleHostWithUiSupport(
                [testHost, testModule],
                out _,
                out _);

            // Act + Assert
            manager.GetModuleAssemblies().Should().ContainSingle(k => k.FullName == testHost.GetType().Assembly.FullName);
            manager.GetModuleAssemblies().Should().ContainSingle(k => k.FullName == testModule.GetType().Assembly.FullName);
        }
    }

    public class MapModuleEndpoints : ModuleHostTests
    {
        [Fact]
        public async Task Should_call_map_endpoints_on_all_modules()
        {
            // Arrange
            var testHost = new TestUiHostBackend();
            var testModule = new TestBackendModule();
            var manager = CreateModuleHostWithUiSupport(
                [testHost, testModule],
                out var services,
                out _);

            await using var provider = services.BuildServiceProvider();
            var endpointBuilder = Substitute.For<IEndpointRouteBuilder>();
            endpointBuilder.ServiceProvider.Returns(provider);

            // Act + Assert
            testModule.CallReceived += (_, s) =>
            {
                s.Should().Be(nameof(BackendModule.MapEndpoints));
            };
            manager.MapModuleEndpoints(endpointBuilder);
        }

        [Fact]
        public async Task Should_setup_message_on_disabled_ui_host()
        {
            // Arrange
            var module = new TestUiHostBackend();
            var manager = CreateModuleHostWithUiSupport(module, out var services, out _);
            await using var serviceProvider = services.BuildServiceProvider();

            var endpointBuilder = Substitute.For<IEndpointRouteBuilder>();
            endpointBuilder.ServiceProvider.Returns(serviceProvider);

            // Act + Assert
            module.CallReceived += (_, s) => s.Should().Be(nameof(ModuleHost.MapModuleEndpoints));
            manager.MapModuleEndpoints(endpointBuilder);
        }
    }

    public class UseUiHost : ModuleHostTests
    {
        [Fact]
        public async Task Should_initialize_with_options()
        {
            // Arrange
            var hostModule = new TestUiHostBackend();
            var webEnv = Substitute.For<IWebHostEnvironment>();
            var hostEnv = Substitute.For<IUiHostEnvironment>();

            var manager = CreateModuleHostWithUiSupport(hostModule, out var services, out _);
            services.AddSingleton(hostEnv);
            await using var serviceProvider = services.BuildServiceProvider();

            var appBuilder = Substitute.For<IApplicationBuilder>();
            appBuilder.ApplicationServices
                .Returns(serviceProvider);

            // Act + Assert
            hostModule.CallReceived += (_, s) => s.Should().Be(nameof(ModuleHost.UseUiHost));
            manager.UseUiHost(appBuilder, webEnv);
        }
    }

    public class UseSecurity : ModuleHostTests
    {
        [Fact]
        public async Task Should_call_module_use_security()
        {
            // Arrange
            var module = new TestUiHostBackend();
            var manager = CreateModuleHostWithUiSupport(module, out var services, out _);
            await using var serviceProvider = services.BuildServiceProvider();

            var appBuilder = Substitute.For<IApplicationBuilder>();
            appBuilder.ApplicationServices
                .Returns(serviceProvider);

            // Act + Assert
            module.CallReceived += (_, s) => s.Should().Be(nameof(ModuleHost.UseSecurity));
            manager.UseSecurity(appBuilder);
        }
    }

    public class UseModuleServices : ModuleHostTests
    {
        [Fact]
        public async Task Should_call_use_services_on_modules()
        {
            // Arrange
            var module = new TestBackendModule();
            var manager = CreateModuleHostWithUiSupport(module, out var services, out _);
            await using var serviceProvider = services.BuildServiceProvider();

            var appBuilder = Substitute.For<IApplicationBuilder>();
            appBuilder.ApplicationServices
                .Returns(serviceProvider);

            // Act + Assert
            module.CallReceived += (_, s) => s.Should().Be(nameof(BackendModule.UseServices));
            manager.UseModuleServices(appBuilder);
        }
    }

    public class ConfigureBusRegistrationConfigurator : ModuleHostTests
    {
        [Fact]
        public void Should_call_module_configure_bus()
        {
            // Arrange
            var module = new TestBackendModule();
            var busConfigurator = Substitute.For<IBusRegistrationConfigurator>();
            var manager = CreateModuleHostWithUiSupport(module, out _, out _);

            // Act + Assert
            module.CallReceived += (_, s) => s.Should().Be(nameof(BackendModule.ConfigureMessageBus));
            manager.ConfigureBusRegistrationConfigurator(busConfigurator);
        }
    }

    public class MoveModuleResources : ModuleHostTests
    {
        [Fact]
        public async Task Module_resources_should_be_copied()
        {
            // Arrange
            var module = new TestBackendModule();
            var config = CreateConfiguration();
            var manager = CreateModuleHost(module, config);
            var testAssembly = Assembly.GetExecutingAssembly();
            var fileSystem = new MockFileSystem();
            var services = new ServiceCollection();
            services.AddSingleton(config);
            services.AddSingleton<IFileSystem>(fileSystem);
            services.AddLogging();
            await using var serviceProvider = services.BuildServiceProvider();

            var testFolder = fileSystem.Path.GetDirectoryName(testAssembly.Location);
            var resourceDirectory = fileSystem.Path.Combine(testFolder!, module.GetResourceDirectory(serviceProvider));
            const string image = "image.png";
            const string markdown = "readme.md";

            fileSystem.AddDirectory(resourceDirectory);
            fileSystem.AddFile(fileSystem.Path.Combine(resourceDirectory, image), new MockFileData("01234"));
            fileSystem.AddFile(fileSystem.Path.Combine(resourceDirectory, markdown), new MockFileData("[Text]"));

            // Act
            manager.MoveModuleResources(serviceProvider);

            // Assert
            var targetDirectory = fileSystem.CreateModuleAppDataDirectory(config.GetInstanceOptions(), module.ModuleKey.ModuleId);
            fileSystem.Path.Exists(fileSystem.Path.Combine(targetDirectory, image)).Should().BeTrue();
            fileSystem.File.Exists(fileSystem.Path.Combine(targetDirectory, markdown)).Should().BeTrue();
        }

        [Fact]
        public async Task Exception_on_module_resource_move_should_be_catched()
        {
            // Arrange
            var module = new TestBackendModule();
            var config = CreateConfiguration();
            var manager = CreateModuleHost(module, config);
            var testAssembly = Assembly.GetExecutingAssembly();

            var fileSystem = Substitute.For<IFileSystem>();
            fileSystem.Path
                .GetDirectoryName(testAssembly.Location)
                .Throws(new DirectoryNotFoundException());

            await using var services = new ServiceCollection()
                .AddSingleton(config)
                .AddSingleton(fileSystem)
                .AddLogging()
                .BuildServiceProvider();

            // Act + Assert
            manager.MoveModuleResources(services);
        }
    }

    public class MigrateAndSeedModuleData : ModuleHostTests
    {
        [Fact]
        public async Task Should_call_module_initializer_methods()
        {
            // Arrange
            var initializer = Substitute.For<IModuleInitializer>();
            var module = new TestBackendModule(initializer);
            var config = CreateConfiguration();
            var manager = CreateModuleHost(module, config);

            await using var services = new ServiceCollection()
                .AddSingleton(config)
                .AddLogging()
                .BuildServiceProvider();

            await using var scope = services.CreateAsyncScope();

            // Act
            await manager.MigrateAndSeedModuleData(scope, config, TestContext.Current.CancellationToken);

            // Assert
            await initializer.Received().OnPreMigrate(Arg.Any<IServiceProvider>(), TestContext.Current.CancellationToken);
            await initializer.Received().Migrate(Arg.Any<IServiceProvider>(), TestContext.Current.CancellationToken);
            await initializer.Received().OnPostMigrate(Arg.Any<IServiceProvider>(), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Should_not_call_post_migrate_on_slave()
        {
            // Arrange
            var initializer = Substitute.For<IModuleInitializer>();
            var module = new TestBackendModule(initializer);
            var config = CreateConfiguration(InstanceType.Slave);
            var manager = CreateModuleHost(module, config);

            await using var services = new ServiceCollection()
                .AddSingleton(config)
                .AddLogging()
                .BuildServiceProvider();

            await using var scope = services.CreateAsyncScope();

            // Act
            await manager.MigrateAndSeedModuleData(scope, config, TestContext.Current.CancellationToken);

            // Assert
            await initializer.Received().OnPreMigrate(Arg.Any<IServiceProvider>(), TestContext.Current.CancellationToken);
            await initializer.Received().Migrate(Arg.Any<IServiceProvider>(), TestContext.Current.CancellationToken);
            await initializer.DidNotReceive().OnPostMigrate(Arg.Any<IServiceProvider>(), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Should_not_throw_on_migration_exception()
        {
            // Arrange
            var initializer = Substitute.For<IModuleInitializer>();
            var module = new TestBackendModule(initializer);
            var config = CreateConfiguration(InstanceType.Slave);
            var manager = CreateModuleHost(module, config);

            initializer
                .OnPreMigrate(Arg.Any<IServiceProvider>(), TestContext.Current.CancellationToken)
                .ThrowsAsync(new InvalidOperationException());

            await using var services = new ServiceCollection()
                .AddSingleton(config)
                .AddLogging()
                .BuildServiceProvider();

            await using var scope = services.CreateAsyncScope();

            // Act + Assert
            await manager.MigrateAndSeedModuleData(scope, config, TestContext.Current.CancellationToken);
        }
    }

    private IConfiguration CreateConfiguration(InstanceType instanceType = InstanceType.Standalone)
        => new TestConfig()
            .AddInstanceOptions(type: instanceType)
            .ConfigureModuleLoader()
            .AddTestUiHost()
            .AddTestBackendClientModule()
            .AddTestModule(nameof(TestModules.ModuleDependsOnTestBackend))
            .BuildConfiguration();

    private ModuleHost CreateModuleHost()
        => CreateModuleHost(
            [
                new TestUiHostBackend(),
                new TestBackendModule(),
                new TestModules.ModuleDependsOnTestBackend(),
                new SystemBackendModule()
            ],
            CreateConfiguration());

    private ModuleHost CreateModuleHost(BackendModule module, IConfiguration? configuration = null)
        => CreateModuleHost(
            [
                module,
            ],
            configuration);

    private ModuleHost CreateModuleHostWithUiSupport(BackendModule module, out IServiceCollection services, out IConfiguration config)
        => CreateModuleHostWithUiSupport(
            [
                module,
            ],
            out services,
            out config);

    private static IMvcBuilder CreateMvcBuilder(IServiceCollection services)
    {
        var partManager = new ApplicationPartManager();
        partManager.FeatureProviders.Add(new ControllerFeatureProvider());

        var mvcBuilder = Substitute.For<IMvcBuilder>();
        mvcBuilder.PartManager.Returns(partManager);
        mvcBuilder.Services.Returns(services);

        return mvcBuilder;
    }

    private ModuleHost CreateModuleHostWithUiSupport(List<BackendModule> modules, out IServiceCollection services, out IConfiguration config)
    {
        config = CreateConfiguration();
        services = new ServiceCollection();
        services
            .AddSingleton(config)
            .AddLogging();

        services.AddOptions<InstanceOptions>()
            .Bind(config.GetSection(InstanceOptions.ConfigSection));

        services.AddOptions<LoggingOptions>()
            .Configure(o => o.LogPath = "LogPath")
            .Bind(config.GetSection(LoggingOptions.ConfigSection));

        services.AddSingleton<IFileSystem, MockFileSystem>();
        services.AddModuleServices();

        var manager = CreateModuleHost(modules, config);
        services.AddSingleton<IModuleHost>(manager);

        return manager;
    }

    private ModuleHost CreateModuleHost(List<BackendModule> modules, IConfiguration? configuration = null)
    {
        var config = configuration ?? CreateConfiguration();

        // Fake loaded modules
        var bundles = modules.Select(m =>
        {
            var assembly = m.GetType().GetAssembly();
            return new ModuleBundle<BackendModule>(m, assembly, assembly.Location);
        });

        // Just fake an empty context - it's not used within these tests
        var suiteContext = new SuiteDependencyContext(new ModuleDependencyContext(ModuleType.Backend, "jsonPath", true),
            null,
            []);

        var hostOptions = new ModuleHostOptions()
        {
            Configuration = config,
            LoadedBundles = bundles ?? [],
            ModuleOptions = [],
            Modules = CreateModuleMetadataBundles(bundles ?? []),
            SuiteContext = suiteContext,
        };

        return new ModuleHost(hostOptions);
    }

    private static List<ModuleMetadataBundle> CreateModuleMetadataBundles(IEnumerable<ModuleBundle<BackendModule>> bundles)
        => bundles.Select(k =>
        {
            // Use assembly name for test dlls - real module id only when Backend|Client suffix is available
            var moduleId = Path.GetFileNameWithoutExtension(k.AssemblyLocation);
            if (moduleId.EndsWith(Constants.ModuleSuffixBackend, StringComparison.OrdinalIgnoreCase)
                || moduleId.EndsWith(Constants.ModuleSuffixClient, StringComparison.OrdinalIgnoreCase))
                moduleId = k.Module.ModuleId;

            return new ModuleMetadataBundle()
            {
                ModuleId = moduleId,
                Metadata = new ModuleMetadata() { MinSuiteSdkVersion = "1.0.0", Name = moduleId, Version = "1.0.0" }

            };
        }).ToList();
}
