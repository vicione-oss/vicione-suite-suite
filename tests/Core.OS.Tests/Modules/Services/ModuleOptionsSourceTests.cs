using Core.OS.Modules;
using Core.OS.Modules.Services;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleOptionsSourceTests
{
    private readonly IModuleHost _moduleManager = Substitute.For<IModuleHost>();
    private readonly IModuleOptionsStore _optionsStore = Substitute.For<IModuleOptionsStore>();

    private ModuleOptionsSource CreateSource()
        => new(_moduleManager, _optionsStore);

    [Fact]
    public void Should_build_module_options_provider()
    {
        // Assert
        var source = CreateSource();

        // Act
        var provider = source.Build(new ConfigurationBuilder());

        // Assert
        provider.Should().BeOfType<ModuleOptionsProvider>();
    }
}
