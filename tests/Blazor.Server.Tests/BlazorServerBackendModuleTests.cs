using System.IO.Abstractions;
using System.Reflection;
using Blazor.Server.Backend;
using Blazor.Server.Backend.Services;
using Blazor.Server.Tests.Helpers;
using Blazor.Shared.Services;
using Core.UiHosting;
using Core.Shared.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using TestModule.Client;

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

    public sealed class LoadUiDependencies : BlazorServerBackendModuleTests
    {
        [Fact]
        public async Task Should_load_ui_dependencies_successfully()
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

    public sealed class RegisterUiDependencies : BlazorServerBackendModuleTests
    {
        [Fact]
        public async Task Should_register_ui_blazor_server_client_module_and_client_service_successfully()
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

            // Substitutes registrations that the missing calls would have added.
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

    public sealed class RevalidateStaticFile : BlazorServerBackendModuleTests
    {
        [Fact]
        public void Should_make_the_browser_revalidate_a_cached_static_file()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var context = new StaticFileResponseContext(httpContext, Substitute.For<Microsoft.Extensions.FileProviders.IFileInfo>());

            // Act
            BlazorServerBackendModule.RevalidateStaticFile(context);

            // Assert
            httpContext.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
        }
    }
}
