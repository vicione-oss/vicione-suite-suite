using Blazor.Shared.SystemInformation.Extensions;
using NSubstitute;
using Sdk.Instance;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.SystemInformation.Extensions;

public sealed class IInstanceInformationProviderExtensionsTests
{
    [Fact]
    public async Task GetSysInfoMd_WhenNoModules_ReturnsExpectedMarkdown_WithNoModulesMessage_AndLFOnly()
    {
        // Arrange
        var provider = Substitute.For<IInstanceInformationProvider>();
        var local = Substitute.For<IInstanceInformation>();

        local.Version.Returns("1.2.0");
        local.Type.Returns(InstanceType.Standalone);
        local.SerialNumber.Returns("e56e0314b1cf4fd680c09e9b540acbd0");
        local.SystemType.Returns(string.Empty);

        provider.Local.Returns(local);
        provider.GetInstalledModules()
            .Returns(Task.FromResult<IReadOnlyCollection<ModuleMetadata>>([]));

        // Act
        var md = await provider.GetSystemInformationMarkdown();

        // Assert
        const string expected = "|Field|Value|\n" +
                                "|---|---|\n" +
                                "| Suite | v1.2.0 |\n" +
                                "| Type | Standalone |\n" +
                                "| Sn. | e56e0314b1cf4fd680c09e9b540acbd0 |\n" +
                                "\n" +
                                "**Modules**\n" +
                                "\n" +
                                "_no modules loaded_\n";

        Assert.Equal(expected, md);

        // Ensure LF only (no CR)
        Assert.DoesNotContain('\r', md);

        await provider.Received(1).GetInstalledModules();
    }

    [Fact]
    public async Task GetSysInfoMd_WhenSystemTypePresent_AppendsSystemTypeInParentheses()
    {
        // Arrange
        var provider = Substitute.For<IInstanceInformationProvider>();
        var local = Substitute.For<IInstanceInformation>();

        local.Version.Returns("2.0.0");
        local.Type.Returns(InstanceType.Standalone);
        local.SerialNumber.Returns("SN123");
        local.SystemType.Returns("MySystem");

        provider.Local.Returns(local);
        provider.GetInstalledModules()
            .Returns(Task.FromResult<IReadOnlyCollection<ModuleMetadata>>([]));

        // Act
        var md = await provider.GetSystemInformationMarkdown();

        // Assert
        Assert.Contains("| Sn. | SN123 (MySystem) |\n", md, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', md);
    }

    [Fact]
    public async Task GetSysInfoMd_WhenModulesExist_RendersModulesTable()
    {
        // Arrange
        var provider = Substitute.For<IInstanceInformationProvider>();
        var local = Substitute.For<IInstanceInformation>();

        local.Version.Returns("1.0.0");
        local.Type.Returns(InstanceType.Standalone);
        local.SerialNumber.Returns("ABC");
        local.SystemType.Returns(string.Empty);

        provider.Local.Returns(local);

        var modules = new[]
        {
            CreateModuleMetadata("ModA", "3.4.5"),
            CreateModuleMetadata("ModB", "0.1.0")
        };

        provider.GetInstalledModules().Returns(Task.FromResult<IReadOnlyCollection<ModuleMetadata>>(modules));

        // Act
        var md = await provider.GetSystemInformationMarkdown();

        // Assert
        Assert.Contains("**Modules**\n\n", md, StringComparison.Ordinal);

        // Modules table header + separator
        Assert.Contains("|Name|Version|\n|---|---|\n", md, StringComparison.Ordinal);

        // Rows
        Assert.Contains("| ModA | v3.4.5 |\n", md, StringComparison.Ordinal);
        Assert.Contains("| ModB | v0.1.0 |\n", md, StringComparison.Ordinal);

        // Still LF-only
        Assert.DoesNotContain('\r', md);
    }

    [Fact]
    public async Task GetSysInfoMd_EscapesPipeCharacters_InFieldsAndModuleData()
    {
        // Arrange
        var provider = Substitute.For<IInstanceInformationProvider>();
        var local = Substitute.For<IInstanceInformation>();

        local.Version.Returns("1|2|3");
        local.Type.Returns(InstanceType.Standalone);
        local.SerialNumber.Returns("SN|X");
        local.SystemType.Returns("SYS|T");

        provider.Local.Returns(local);

        var modules = new[]
        {
            CreateModuleMetadata("Name|With|Pipes", "Ver|1")
        };

        provider.GetInstalledModules().Returns(Task.FromResult<IReadOnlyCollection<ModuleMetadata>>(modules));

        // Act
        var md = await provider.GetSystemInformationMarkdown();

        // Assert (pipes escaped in fields that go through EscapeMd)
        Assert.Contains("| Suite | v1\\|2\\|3 |\n", md, StringComparison.Ordinal);
        Assert.Contains("| Sn. | SN\\|X (SYS\\|T) |\n", md, StringComparison.Ordinal);

        // Pipes escaped in module name/version
        Assert.Contains("| Name\\|With\\|Pipes | vVer\\|1 |\n", md, StringComparison.Ordinal);

        // Sanity: still a table row (unescaped pipes would increase column count visually)
        Assert.DoesNotContain('\r', md);
    }

    private static ModuleMetadata CreateModuleMetadata(string name, string version)
        => new()
        {
            Name = name,
            Version = version,
            MinSuiteSdkVersion = "0.0.0"
        };
}
