using Core.OS.Modules;
using Core.OS.Modules.Services;
using TestModule.Backend;
using TestModule.Client;

namespace Core.OS.Tests.Modules.Services;

public class ModuleOptionsProviderTests
{
    private readonly IModuleHost _moduleManager = Substitute.For<IModuleHost>();
    private readonly IModuleOptionsStore _optionsStore = Substitute.For<IModuleOptionsStore>();
    private ModuleOptionsProvider CreateProvider()
        => new(_moduleManager, _optionsStore);

    [Fact]
    public void Data_should_contain_options_of_backend_modules()
    {
        // Assert
        var provider = CreateProvider();
        var options = new Dictionary<string, string?>
        {
            ["option1"] = "value1",
            ["option2"] = "42",
            ["option3"] = "value3",
        };

        _moduleManager.GetModules().Returns([new TestBackendModule(), new TestClientModule()]);
        _optionsStore.LoadDictionarySync(Arg.Any<string>()).Returns(options);

        // Act
        provider.Load();

        // Assert
        foreach (var option in options)
        {
            provider.TryGet(option.Key, out var value).Should().BeTrue();
            value.Should().Be(option.Value);
        }

        provider.GetChildKeys([], null).Should().HaveCount(options.Count);
    }
}
