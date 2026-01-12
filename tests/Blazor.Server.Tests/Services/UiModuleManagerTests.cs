using System.Reflection;
using Blazor.Server.Backend.Services;
using Core.UiHosting;
using AwesomeAssertions;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Sdk.Client.Modules;
using Sdk.Modules;
using TestModule.Client;
using Xunit;

namespace Blazor.Server.Tests.Services;

public class UiModuleManagerTests
{
    private readonly Assembly _testModuleClientAssembly = typeof(TestClientModule).Assembly;

    private int _countUiClientModules;
    private int _countUiClientModuleAssemblies;

    [Fact]
    public void Returns_ui_client_modules()
    {
        // Arrange
        var uiModuleManager = SetupTest();

        // Act
        var uiModules = uiModuleManager.UiModules;

        // Assert
        uiModules.Count().Should().Be(_countUiClientModules);
    }

    [Fact]
    public void Returns_ui_client_module_assemblies()
    {
        // Arrange
        var uiModuleManager = SetupTest();

        // Act
        var uiModuleAssemblies = uiModuleManager.UiModuleAssemblies;

        // Assert
        uiModuleAssemblies.Count().Should().Be(_countUiClientModuleAssemblies);
    }

    private UiModuleManager SetupTest()
    {
        var uiModulManager = new UiModuleManager();
        var uiBundles = new List<UiModuleBundle>
        {
            new(new TestClientModule(), _testModuleClientAssembly.GetAssemblyLocation(), _testModuleClientAssembly),
            new(new TestBlazorServerClientModule(), _testModuleClientAssembly.GetAssemblyLocation(), _testModuleClientAssembly),
        };
        _countUiClientModules += 2;
        _countUiClientModuleAssemblies = uiBundles.Count;

        uiModulManager.AddModuleBundles(uiBundles);
        return uiModulManager;
    }

    private class UiModuleBundle(ClientModule module, string assemblyLocation, Assembly? assembly) : IUiModuleBundle
    {
        public IModule Module { get; } = module;
        public string AssemblyLocation { get; } = assemblyLocation;
        public Assembly? Assembly { get; } = assembly;
    }
}
