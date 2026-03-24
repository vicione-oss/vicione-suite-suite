using Xunit;
using AwesomeAssertions;
using System.IO.Abstractions;
using Sdk.Testing.Extensions;
using HostManagement.Shared.Contracts.Network;
using Semver;

namespace Core.OS.Tests.HostManagement;

public class HostManagmentVersionTests
{
    [Fact]
    public void Should_return_non_empty_version_string()
    {
        // Arrange
        var fileSystem = new FileSystem();
        var repoRoot = fileSystem.GetRepositoryRootPath();
        var hostMgmtVersionFilePath = fileSystem.Path.Combine(repoRoot, "VERSION_HOSTMANAGEMENT");

        var hostManagementAssemblyName = typeof(NetworkDNSSettings).Assembly.GetName();
        if (hostManagementAssemblyName.Version is null)
            throw new InvalidOperationException("Failed to get HostManagement version");

        var fileVersionString = fileSystem.File.ReadAllText(hostMgmtVersionFilePath).TrimEnd();
        if (!SemVersion.TryParse(fileVersionString, out var fileVersion))
            throw new InvalidOperationException("Failed to parse HOSTMANAGEMENT version");

        // Act
        var hmVersion = SemVersion.FromVersion(hostManagementAssemblyName.Version);

        // Assert
        fileVersion.IsPrerelease.Should().BeFalse();
        hmVersion.Major.Should().Be(fileVersion.Major);
        hmVersion.Minor.Should().Be(fileVersion.Minor);
        hmVersion.Patch.Should().Be(fileVersion.Patch);
    }
}
