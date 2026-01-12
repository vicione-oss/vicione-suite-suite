using System.Reflection;
using Blazor.Server.Backend.Services;
using Core.UiHosting;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Sdk.Client.Modules;
using Sdk.Modules;
using TestModule.Client;
using Xunit;
using Blazor.Server.Backend;

namespace Blazor.Server.Tests.Services;

public class UiModuleManagerTests
{
    private readonly Assembly _testModuleClientAssembly = typeof(TestClientModule).Assembly;
    private int _uiModuleBundleCount;

    private UiModuleManager SetupTest()
    {
        var uiModulManager = new UiModuleManager();
        var uiBundles = new List<UiModuleBundle>
        {
            new(new TestClientModule(), _testModuleClientAssembly.GetAssemblyLocation(), _testModuleClientAssembly),
            new(new TestBlazorServerClientModule(), _testModuleClientAssembly.GetAssemblyLocation(), _testModuleClientAssembly),
        };
        _uiModuleBundleCount = uiBundles.Count;

        uiModulManager.AddModuleBundles(uiBundles);
        return uiModulManager;
    }

    public class UiModules : UiModuleManagerTests
    {
        [Fact]
        public void Returns_ui_client_modules()
        {
            // Arrange
            var uiModuleManager = SetupTest();

            // Act
            var uiModules = uiModuleManager.UiModules;

            // Assert
            uiModules.Count().Should().Be(_uiModuleBundleCount);
        }
    }

    public class UiModuleAssemblies : UiModuleManagerTests
    {
        [Fact]
        public void Returns_ui_client_module_assemblies()
        {
            // Arrange
            var uiModuleManager = SetupTest();

            // Act
            var uiModuleAssemblies = uiModuleManager.UiModuleAssemblies;

            // Assert
            uiModuleAssemblies.Count().Should().Be(_uiModuleBundleCount - 1, "2 bundles but same assembly");
        }
    }

    public class GetAdditionalAssemblies : UiModuleManagerTests
    {
        [Fact]
        public void Should_returns_additional_assemblies()
        {
            // Arrange
            var uiModuleManager = SetupTest();

            // Act
            var uiModuleAssemblies = uiModuleManager.GetAdditionalAssemblies();

            // Assert
            uiModuleAssemblies.Count().Should().Be(2, "Client + Blazor.Server.Backend assemblies");
        }

        [Fact]
        public void Should_provide_blazor_server_backend_assembly()
        {
            // Arrange
            var uiModuleManager = SetupTest();

            // Act
            var uiModuleAssemblies = uiModuleManager.GetAdditionalAssemblies();

            // Assert
            uiModuleAssemblies.Contains(typeof(BlazorServerBackendModule).Assembly);
        }
    }

    private class UiModuleBundle(ClientModule module, string assemblyLocation, Assembly? assembly) : IUiModuleBundle
    {
        public IModule Module { get; } = module;
        public string AssemblyLocation { get; } = assemblyLocation;
        public Assembly? Assembly { get; } = assembly;
    }
}
