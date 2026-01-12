using Core.OS.Extensions;
using Core.OS.Modules;
using Core.OS.Tests.Extensions;
using Core.Tests.Tools;
using AwesomeAssertions;
using Core.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Extensions;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using TestUiHost;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleLoaderOptionsTests
{
    [Fact]
    public void Get_module_settings_should_throw_on_missing_section()
    {
        // Arrange
        var config = new TestConfig().BuildConfiguration();

        // Act + Assert
        Assert.Throws<ConfigurationException>(config.GetModuleLoaderOptions);
    }

    [Fact]
    public void Add_module_settings_should_configure_settings()
    {
        // Arrange
        var manifestProvider = Substitute.For<IModuleManifestProvider>();
        manifestProvider.GetManifest().Returns(new ModulePackageManifest());

        var config = new TestConfig()
            .ConfigureModuleLoader()
            .AddTestBackendClientModule()
            .BuildConfiguration();

        // Act
        var loaderOptions = config.GetModuleLoaderOptions();
        var moduleOptions = config.CreateModuleOptions(manifestProvider, loaderOptions, ModuleIdResolver.ResolveId<TestBackendModule>());

        // Assert
        Assert.NotNull(moduleOptions[ModuleIdResolver.ResolveId<TestBackendModule>()]);
    }

    [Fact]
    public void Configuration_should_work()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddCoreAppSettings("Development");

        // Act
        var loaderOptions = builder.Build().GetModuleLoaderOptions();

        // Assert
        loaderOptions.UiHost.Should().NotBeNull().And.NotBeEmpty();
    }

    [Fact]
    public void Get_custom_module_options_from_configuration()
    {
        // Arrange
        var customOptions = new TestOptions
        {
            Flag = true,
            StringValue = "TestMe",
            IntValue = 5001,
        };
        var config = new TestConfig().AddModuleWithOptions(ModuleIdResolver.ResolveId<TestBackendModule>(), customOptions).BuildConfiguration();

        // Act
        var configOptions = config.BindModuleSection<TestOptions>(ModuleIdResolver.ResolveId<TestBackendModule>())!;

        // Assert
        Assert.Equal(customOptions.Flag, configOptions.Flag);
        Assert.Equal(customOptions.StringValue, configOptions.StringValue);
        Assert.Equal(customOptions.IntValue, configOptions.IntValue);
    }

    [Fact]
    public void Get_custom_module_options_from_service_provider()
    {
        // Arrange
        var customOptions = new TestOptions
        {
            Flag = true,
            StringValue = "TestMe",
            IntValue = 5001,
        };
        var config = new TestConfig().AddModuleWithOptions(ModuleIdResolver.ResolveId<TestBackendModule>(), customOptions);
        var serviceProvider = new ServiceCollection()
            .AddConfiguration(config)
            .AddModuleSection<TestOptions>(ModuleIdResolver.ResolveId<TestBackendModule>().Replace(".", "", StringComparison.Ordinal))
            .BuildServiceProvider();

        // Act
        var serviceOptions = serviceProvider.GetRequiredService<IOptions<TestOptions>>().Value;

        // Assert
        Assert.NotNull(serviceOptions);
        Assert.Equal(customOptions.Flag, serviceOptions.Flag);
        Assert.Equal(customOptions.StringValue, serviceOptions.StringValue);
        Assert.Equal(customOptions.IntValue, serviceOptions.IntValue);
    }

    [Fact]
    public void Get_custom_ui_host_options_from_configuration()
    {
        // Arrange
        var options = new TestOptions
        {
            Flag = true,
            IntValue = 111,
            StringValue = "SomeVal",
        };

        var config = new TestConfig()
            .AddTestUiHost()
            .AddModuleWithOptions(ModuleIdResolver.ResolveId<TestUiHostBackend>(), options);

        // Act
        var configOptions = config.BuildConfiguration().BindModuleSection<TestOptions>(ModuleIdResolver.ResolveId<TestUiHostBackend>());

        // Assert
        configOptions.Flag.Should().BeTrue();
        configOptions.IntValue.Should().Be(options.IntValue);
        configOptions.StringValue.Should().Be(options.StringValue);
    }

    public class TestOptions : Module.Contracts.ModuleOptions
    {
        public bool Flag { get; init; }
        public string? StringValue { get; init; }
        public int IntValue { get; init; }
    }
}
