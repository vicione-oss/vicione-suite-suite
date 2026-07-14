using Core.Module.Extensions;
using Core.Shared.Modules;
using Xunit;

namespace Core.Module.Tests;

public class ModuleBundlerTests
{
    [Fact]
    public async Task Should_zip_client_modules_with_dependencies()
    {
        // Arrange
        var suiteContext = TestFactory.CreateSuiteContext(enableBackendModules: false);

        // Act
        var dependencies = suiteContext.GetAllUiRuntimeDependencies(SkipForTest);
        var zipData = await ModuleZip.CreateArchive(dependencies);
        var unzipped = await ModuleZip.ExtractArchive(zipData, []);

        // Assert
        Assert.True(dependencies.Count == unzipped.Dlls.Count);
    }

    private static bool SkipForTest(string dllName)
    {
        if (!File.Exists(dllName))
            return true;

        var ignoreStartingWith = new[]
        {
            "Microsoft.",
            "System.",
            "Duende.",
            "Serilog",
            "Npgsql",
            "MQTT",
            "SQLite",
            "nunit",
            "xunit",
            "testhost",
            "Fluent",
            "AngleSharp",
            "Bunit",
            "Castle",
            "Auto",
            "NuGet",
            "Moq",
            "MassTransit"
        };

        // Ignore all these dlls to speed up the test
        var name = Path.GetFileNameWithoutExtension(dllName);

        return ignoreStartingWith.Any(k => name.StartsWith(k, StringComparison.Ordinal));
    }
}
