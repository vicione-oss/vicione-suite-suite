using System.IO.Abstractions;
using System.Reflection;
using Blazor.Server.Backend;
using Blazor.Server.Backend.Services;
using Blazor.Server.Tests.Helpers;
using Blazor.Shared.Services;
using Core.UiHosting;
using AwesomeAssertions;
using Core.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using TestModule.Client;
using Xunit;

namespace Blazor.Server.Tests;

public class BlazorServerBackendModuleTests
{
    private readonly IUiHostEnvironment _uiHostEnv = Substitute.For<IUiHostEnvironment>();

    private static (IServiceCollection, BlazorServerBackendModule) SetupTest()
    {
        var serviceCollection = new ServiceCollection()
            .AddConfiguration()
            .AddMvcBuilder()
            .AddEndpointRouteBuilder()
            .AddLogging();

        return (serviceCollection, new BlazorServerBackendModule());
    }

    public class LoadUiDependencies : BlazorServerBackendModuleTests
    {
        [Fact]
        public async Task Load_ui_dependencies_is_successful()
        {
            // Arrange
            var (serviceCollection, backend) = SetupTest();

            var uiModuleBundles = BlazorServerHelpers.GetUiModuleBundlesWithUiModules();
            _uiHostEnv.ModulePath.Returns("TestModulePath");
            _uiHostEnv.LoadModuleBundles(Arg.Any<Func<string, Assembly, IUiModuleBundle?>>())
                .Returns(uiModuleBundles);

            // Act
            backend.LoadUiDependencies(serviceCollection, _uiHostEnv);
            await using var serviceProvider = serviceCollection.BuildServiceProvider();

            // Assert
            var moduleManagerFromInterface = serviceProvider.GetRequiredService<IUiModuleManager>();
            moduleManagerFromInterface.Should().NotBeNull();
            var moduleManagerFromInstance = serviceProvider.GetRequiredService<UiModuleManager>();
            moduleManagerFromInstance.Should().NotBeNull();

            moduleManagerFromInstance.UiModules.Should().Contain(m => Equals(m.ModuleId, TestClientModule.Id));
        }
    }

    public class RegisterUiDependencies : BlazorServerBackendModuleTests
    {
        [Fact]
        public async Task Register_ui_blazor_server_client_module_and_client_service_is_successful()
        {
            // Arrange
            var (serviceCollection, backend) = SetupTest();

            var uiModuleBundles = BlazorServerHelpers.GetUiModuleBundlesWithBlazorServerUiModules();
            _uiHostEnv.ModulePath.Returns("TestModulePath");
            _uiHostEnv.LoadModuleBundles(Arg.Any<Func<string, Assembly, IUiModuleBundle?>>())
                .Returns(uiModuleBundles);

            // Act
            backend.LoadUiDependencies(serviceCollection, _uiHostEnv);
            backend.ConfigureUiServices(serviceCollection, _uiHostEnv);

            serviceCollection.AddWorkspaceService<BlazorServerBackendModule>(); // adopted from ModuleManager.AddModuleServices()

            // substitute missing registrations resulting from missing calls to ...
            serviceCollection.AddSingleton(Substitute.For<ILogLevelSwitch>()); // ... LoggingConfiguration.ConfigureLogging()
            serviceCollection.AddSingleton(Substitute.For<IFileSystem>());
            serviceCollection.AddTransient(sp => Substitute.For<ILogOptions>()); // ...ConfigureAndValidateOptions()

            await using var serviceProvider = serviceCollection.BuildServiceProvider();

            // Assert
            var moduleManagerFromInterface = serviceProvider.GetRequiredService<IUiModuleManager>();
            moduleManagerFromInterface.Should().NotBeNull();
            var moduleManagerFromInstance = serviceProvider.GetRequiredService<UiModuleManager>();
            moduleManagerFromInstance.Should().NotBeNull();

            var uiModules = moduleManagerFromInstance.UiModules.ToList();
            var uiClientModule = uiModules.FirstOrDefault(k => k is TestBlazorServerClientModule);
            uiClientModule.Should().NotBeNull();

            var clientService = serviceProvider.GetRequiredService<IBackendLogService>();
            clientService.Should().NotBeNull();
        }
    }
}
