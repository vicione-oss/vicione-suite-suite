using System.Runtime.InteropServices;

namespace Core.Module.Tests;

public class ArtifactPlatformTests
{
    public sealed class ModuleRuntimeIdentifier
    {
        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.ModulePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public void Should_return_the_portable_runtime_identifier_used_in_module_package_names(string osPlatform, Architecture osArchitecture,
            string expectedRuntimeIdentifier)
        {
            // Arrange
            var platform = ArtifactPlatformTheoryData.Create(osPlatform, osArchitecture);

            // Act
            var runtimeIdentifier = platform.ModuleRuntimeIdentifier;

            // Assert - 0.28.0-ci1523472-linux-arm64.zip
            runtimeIdentifier.Should().Be(expectedRuntimeIdentifier);
        }

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.UnsupportedModulePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public void Should_throw_for_a_platform_without_module_packages(string osPlatform, Architecture osArchitecture)
        {
            // Arrange
            var platform = ArtifactPlatformTheoryData.Create(osPlatform, osArchitecture);

            // Act
            var act = () => platform.ModuleRuntimeIdentifier;

            // Assert
            act.Should().Throw<PlatformNotSupportedException>();
        }

        [Fact]
        public void Should_return_a_portable_runtime_identifier_for_the_current_machine()
        {
            // Act
            var runtimeIdentifier = ArtifactPlatform.Current.ModuleRuntimeIdentifier;

            // Assert
            runtimeIdentifier.Should().MatchRegex("^(linux|win)-(x64|arm64)$");
        }
    }

    public sealed class SuitePackageArchitecture
    {
        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.SuitePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public void Should_return_the_debian_architecture_used_in_suite_package_names(Architecture osArchitecture, string expectedArchitecture)
        {
            // Arrange
            var platform = ArtifactPlatformTheoryData.Create("LINUX", osArchitecture);

            // Act
            var architecture = platform.SuitePackageArchitecture;

            // Assert - vicione-suite_1.0.3_amd64.deb
            architecture.Should().Be(expectedArchitecture);
        }

        [Theory]
        [MemberData(nameof(ArtifactPlatformTheoryData.UnsupportedSuitePlatforms), MemberType = typeof(ArtifactPlatformTheoryData))]
        public void Should_throw_for_an_architecture_without_suite_packages(Architecture osArchitecture)
        {
            // Arrange
            var platform = ArtifactPlatformTheoryData.Create("LINUX", osArchitecture);

            // Act
            var act = () => platform.SuitePackageArchitecture;

            // Assert
            act.Should().Throw<PlatformNotSupportedException>();
        }
    }
}
