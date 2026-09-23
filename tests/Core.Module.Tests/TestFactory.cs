using Core.Tests.Tools;
using Sdk.Testing.Backend;
using TestSystem.Backend;

namespace Core.Module.Tests;

internal static class TestFactory
{
    /// <summary>
    /// Create a suite context with Core, optional TestUiHostBackend, optional TestBackendModule, optional TestClientModule
    /// </summary>
    internal static SuiteDependencyContext CreateSuiteContext(bool enableUiHost = true, bool enableBackendModules = true, bool enableUiModules = true)
    {
        var setup = new TestConfig()
            .ConfigureModuleLoader()
            .AddTestUiHost(enableUiHost)
            .AddTestBackendClientModule(enableBackendModules || enableUiModules);

        var config = setup.BuildConfiguration();
        var loaderOptions = config.GetModuleLoaderTestOptions();
        var moduleOptions = config.CreateModuleTestOptions(loaderOptions);

        var builder = new SuiteDependencyContextBuilder()
            .WithCore(typeof(TestSystemModule).Assembly)
            .WithUiHost(loaderOptions, moduleOptions)
            .WithBackendModules(loaderOptions, moduleOptions);

        if (enableUiModules)
            builder.WithClientModules(loaderOptions, moduleOptions);

        return builder.Build();
    }
}
