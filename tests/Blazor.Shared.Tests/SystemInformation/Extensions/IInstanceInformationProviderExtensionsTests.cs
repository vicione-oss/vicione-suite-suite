using AwesomeAssertions;
using Blazor.Shared.SystemInformation.Extensions;
using NSubstitute;
using Sdk.Instance;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.SystemInformation.Extensions;

public sealed class IInstanceInformationProviderExtensionsTests
{
    [Fact]
    public async Task Should_return_markdown_with_no_modules_message_and_lf_only_when_no_modules()
    {
        // Arrange
        var provider = Substitute.For<IInstanceInformationProvider>();
        var local = Substitute.For<IInstanceInformation>();

        local.Version.Returns("1.2.0");
        local.Type.Returns(InstanceType.Standalone);
        local.SerialNumber.Returns("e56e0314b1cf4fd680c09e9b540acbd0");
        local.SystemType.Returns(string.Empty);

        provider.Local.Returns(local);
        provider.GetInstalledModules(Arg.Any<CancellationToken>())
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

        await provider.Received(1).GetInstalledModules(Arg.Any<CancellationToken>());
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
        provider.GetInstalledModules(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<ModuleMetadata>>([]));

        // Act
        var md = await provider.GetSystemInformationMarkdown();

        // Assert
        md.Should().Contain("| Sn. | SN123 (MySystem) |\n");
        md.Should().NotContain("\r");
    }

    [Fact]
    public async Task Should_render_modules_table_when_modules_exist()
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

        provider.GetInstalledModules(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<ModuleMetadata>>(modules));

        // Act
        var md = await provider.GetSystemInformationMarkdown();

        // Assert
        md.Should().Contain("**Modules**\n\n");

        // Modules table header + separator
        md.Should().Contain("|Name|Version|\n|---|---|\n");

        // Rows
        md.Should().Contain("| ModA | v3.4.5 |\n");
        md.Should().Contain("| ModB | v0.1.0 |\n");

        // Still LF-only
        md.Should().NotContain("\r");
    }

    [Fact]
    public async Task Should_escape_pipe_characters_in_fields_and_module_data()
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

        provider.GetInstalledModules(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<ModuleMetadata>>(modules));

        // Act
        var md = await provider.GetSystemInformationMarkdown();

        // Assert (pipes escaped in fields that go through EscapeMd)
        md.Should().Contain("| Suite | v1\\|2\\|3 |\n");
        md.Should().Contain("| Sn. | SN\\|X (SYS\\|T) |\n");

        // Pipes escaped in module name/version
        md.Should().Contain("| Name\\|With\\|Pipes | vVer\\|1 |\n");

        // Sanity: still a table row (unescaped pipes would increase column count visually)
        md.Should().NotContain("\r");
    }

    private static ModuleMetadata CreateModuleMetadata(string name, string version)
        => new()
        {
            Name = name,
            Version = version,
            MinSuiteSdkVersion = "0.0.0"
        };
}
