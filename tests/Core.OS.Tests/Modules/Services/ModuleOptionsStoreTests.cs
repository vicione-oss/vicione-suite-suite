using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Core.OS.Modules.Extensions;
using Core.OS.Modules.Services;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleOptionsStoreTests
{
    private const double _numberValue = 4.3d;
    private const string _textValue = "ThisIsJustText";
    private const bool _boolValue = true;

    private readonly IServiceProvider _services;
    private readonly MockFileSystem _fileSystem = new();

    private readonly ModuleOptionDeclaration _numberOption = new()
    {
        Key = "MyIntVal",
        OptionType = ModuleOptionType.Number,
        Value = _numberValue.ToString(CultureInfo.InvariantCulture),
    };

    private readonly ModuleOptionDeclaration _textOption = new()
    {
        Key = "MyTextVal",
        OptionType = ModuleOptionType.Text,
        Value = _textValue
    };

    private readonly ModuleOptionDeclaration _boolOption = new()
    {
        Key = "MyBoolVal",
        OptionType = ModuleOptionType.Boolean,
        Value = _boolValue.ToString(CultureInfo.InvariantCulture)
    };

    public ModuleOptionsStoreTests()
    {
        var config = new TestConfig()
            .AddInstanceOptions()
            .BuildConfiguration();

        _services = new ServiceCollection()
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(config)
            .AddSingleton<ModuleOptionsStore>()
            .BuildServiceProvider();
    }

    public class Store : ModuleOptionsStoreTests
    {
        [Fact]
        public async Task Should_create_configuration_file_as_json()
        {
            // Arrange        
            var optionStore = _services.GetRequiredService<ModuleOptionsStore>();

            // Act
            await optionStore.Store(ModuleIdResolver.ResolveId<TestBackendModule>(), [_boolOption, _numberOption, _textOption], CancellationToken.None);

            // Assert            
            _fileSystem.AllFiles.Should().ContainSingle(k => k.EndsWith(ModuleOptionsStore.ModuleSettingsFileName, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_create_configuration_directory()
        {
            // Arrange        
            var optionStore = _services.GetRequiredService<ModuleOptionsStore>();

            // Act
            await optionStore.Store(ModuleIdResolver.ResolveId<TestBackendModule>(), [_boolOption, _numberOption, _textOption], CancellationToken.None);

            // Assert            
            _fileSystem.AllDirectories.Should().ContainSingle(k => k.EndsWith(ModuleIdResolver.ResolveId<TestBackendModule>(), StringComparison.Ordinal));
        }

        [Fact]
        public async Task Should_not_write_configuration_file_without_options()
        {
            // Arrange        
            var optionStore = _services.GetRequiredService<ModuleOptionsStore>();

            // Act
            await optionStore.Store(ModuleIdResolver.ResolveId<TestBackendModule>(), [], CancellationToken.None);

            // Assert            
            _fileSystem.AllFiles.Should().BeEmpty();
        }
    }

    public class Load : ModuleOptionsStoreTests
    {
        [Fact]
        public async Task Should_load_configuration()
        {
            // Arrange        
            var optionStore = _services.GetRequiredService<ModuleOptionsStore>();
            await optionStore.Store(ModuleIdResolver.ResolveId<TestBackendModule>(), [_boolOption, _numberOption, _textOption], CancellationToken.None);

            // Act
            var configuration = await optionStore.LoadJsonConfiguration(ModuleIdResolver.ResolveId<TestBackendModule>());

            // Assert
            Assert.NotNull(configuration);
        }

        [Fact]
        public async Task Should_load_real_configuration()
        {
            // Arrange        
            var optionStore = _services.GetRequiredService<ModuleOptionsStore>();
            await optionStore.Store(ModuleIdResolver.ResolveId<TestBackendModule>(), [_boolOption, _numberOption, _textOption], CancellationToken.None);

            // Act
            var configuration = await optionStore.LoadJsonConfiguration(ModuleIdResolver.ResolveId<TestBackendModule>());

            // Assert
            Assert.NotNull(configuration);
            configuration.GetValue<bool>(_boolOption.GetOptionKey(ModuleIdResolver.ResolveId<TestBackendModule>())).Should().Be(_boolValue);
            configuration.GetValue<double>(_numberOption.GetOptionKey(ModuleIdResolver.ResolveId<TestBackendModule>())).Should().Be(_numberValue);
            configuration.GetValue<string>(_textOption.GetOptionKey(ModuleIdResolver.ResolveId<TestBackendModule>())).Should().Be(_textValue);
        }
    }

    public class ValidateModuleOptions : ModuleOptionsStoreTests
    {
        [Fact]
        public async Task Should_validate_possible_startup()
        {
            // Arrange            
            var optionStore = _services.GetRequiredService<ModuleOptionsStore>();
            var suiteContext = TestFactory.CreateSuiteContext();

            // Act
            await optionStore.ValidateModuleOptions(suiteContext, CancellationToken.None);

            // Assert            
            suiteContext.Modules.SelectMany(k => k.StartupErrors).Should().HaveCount(2, "TestClient|Backend metadata is missing");
        }
    }
}
