using System.IO.Abstractions.TestingHelpers;
using Core.OS.EnvironmentOverrides;
using Core.Shared.EnvironmentOverrides;

namespace Core.OS.Tests.EnvironmentOverrides;

[Collection(EnvironmentOverridesCollectionDefinition.Name)]
public sealed class EnvironmentOverridesFileTests : IDisposable
{
    private const string FileName = "env-overrides.env";

    private static readonly string HomeDirectory =
        OperatingSystem.IsWindows() ? @"C:\var\lib\vicione-suite\AppData" : "/var/lib/vicione-suite/AppData";

    private readonly string? _previousEnv =
        Environment.GetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable);

    private readonly MockFileSystem _fileSystem = new();

    public void Dispose()
        => Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, _previousEnv);

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData(" true ")]
    public void Should_resolve_the_file_in_the_home_directory_when_switched_on(string configured)
    {
        // Arrange
        Enable(configured);

        // Act
        var path = EnvironmentOverridesFile.ResolvePath(_fileSystem, HomeDirectory);

        // Assert
        path.Should().Be(Path.Combine(HomeDirectory, FileName));
    }

    [Fact]
    public void Should_root_a_relative_home_directory()
    {
        // Arrange — development instances configure their home directory relative to the
        // application, and the file has to land in the same place regardless of the working
        // directory the process was launched from.
        Enable();

        // Act
        var path = EnvironmentOverridesFile.ResolvePath(_fileSystem, "AppData_Standalone");

        // Assert
        path.Should().NotBeNull();
        Path.IsPathFullyQualified(path).Should().BeTrue();
        path.Should().EndWith(Path.Combine("AppData_Standalone", FileName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("yes")]
    public void Should_resolve_no_path_when_not_switched_on(string? configured)
    {
        // Arrange
        Enable(configured);

        // Act
        var path = EnvironmentOverridesFile.ResolvePath(_fileSystem, HomeDirectory);

        // Assert
        path.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_resolve_no_path_when_no_home_directory_is_configured(string? homeDirectory)
    {
        // Arrange — an instance without a home directory fails options validation moments later;
        // until then there is no location to put the file in.
        Enable();

        // Act
        var path = EnvironmentOverridesFile.ResolvePath(_fileSystem, homeDirectory);

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public void Should_require_a_path_when_switched_on()
    {
        // Arrange
        Enable();

        // Act
        var path = EnvironmentOverridesFile.RequirePath(_fileSystem, HomeDirectory);

        // Assert
        path.Should().Be(Path.Combine(HomeDirectory, FileName));
    }

    [Fact]
    public void Should_refuse_to_require_a_path_when_not_switched_on()
    {
        // Arrange — answering with a path anyway would let a save write a file that nothing
        // applies at startup.
        Enable(null);

        // Act
        var act = () => EnvironmentOverridesFile.RequirePath(_fileSystem, HomeDirectory);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    private static void Enable(string? configured = "true")
        => Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, configured);
}
