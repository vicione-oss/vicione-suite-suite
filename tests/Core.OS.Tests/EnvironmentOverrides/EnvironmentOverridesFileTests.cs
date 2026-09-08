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

    [Fact]
    public void Should_resolve_the_disabled_file_under_one_fixed_name_next_to_the_override_file()
    {
        // Arrange
        Enable("true");

        // Act
        var path = EnvironmentOverridesFile.ResolvePath(_fileSystem, HomeDirectory);
        var disabledPath = EnvironmentOverridesFile.ResolveDisabledPath(_fileSystem, HomeDirectory);

        // Assert - nothing resolves the disabled name for reading, which is what keeps the file
        // inert on the next boot.
        disabledPath.Should().Be($"{path}.disabled");
    }

    [Fact]
    public void Should_not_resolve_a_disabled_file_when_switched_off()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentOverridesSwitch.EnabledEnvironmentVariable, null);

        // Act
        var disabledPath = EnvironmentOverridesFile.ResolveDisabledPath(_fileSystem, HomeDirectory);

        // Assert
        disabledPath.Should().BeNull();
    }

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
    public void Should_move_the_override_file_to_the_disabled_path()
    {
        // Arrange
        Enable();
        var path = EnvironmentOverridesFile.RequirePath(_fileSystem, HomeDirectory);
        _fileSystem.AddFile(path, new MockFileData("A=b"));

        // Act
        var disabledPath = EnvironmentOverridesFile.Disable(_fileSystem, HomeDirectory);

        // Assert - a rename rather than a delete, so what broke the boot stays readable.
        disabledPath.Should().Be(EnvironmentOverridesFile.ResolveDisabledPath(_fileSystem, HomeDirectory));
        _fileSystem.File.Exists(path).Should().BeFalse();
        _fileSystem.File.ReadAllText(disabledPath).Should().Be("A=b");
    }

    [Fact]
    public void Should_replace_a_disabled_file_left_by_an_earlier_attempt()
    {
        // Arrange - one fixed name means a second disable finds the first one still lying there.
        Enable();
        var path = EnvironmentOverridesFile.RequirePath(_fileSystem, HomeDirectory);
        _fileSystem.AddFile(path, new MockFileData("A=broke-this-boot"));
        _fileSystem.AddFile(
            EnvironmentOverridesFile.ResolveDisabledPath(_fileSystem, HomeDirectory)!,
            new MockFileData("A=left-by-an-earlier-attempt"));

        // Act
        var disabledPath = EnvironmentOverridesFile.Disable(_fileSystem, HomeDirectory);

        // Assert - the current overrides are the ones that broke this boot, so they are the ones
        // worth keeping around.
        _fileSystem.File.Exists(path).Should().BeFalse();
        _fileSystem.File.ReadAllText(disabledPath).Should().Be("A=broke-this-boot");
    }

    [Fact]
    public void Should_report_a_missing_override_file_when_disabling()
    {
        // Arrange - the page only offers the action while the file is there, so this is the
        // resubmitted POST after a first disable already moved it aside.
        Enable();

        // Act
        var act = () => EnvironmentOverridesFile.Disable(_fileSystem, HomeDirectory);

        // Assert - the caller on the failsafe page logs this and restarts anyway; the file being
        // gone is the outcome it wanted.
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Should_refuse_to_disable_when_not_switched_on()
    {
        // Arrange
        Enable(null);

        // Act
        var act = () => EnvironmentOverridesFile.Disable(_fileSystem, HomeDirectory);

        // Assert
        act.Should().Throw<InvalidOperationException>();
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
