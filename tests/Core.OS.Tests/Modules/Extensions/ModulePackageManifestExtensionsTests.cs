using Core.OS.Modules.Extensions;
using Microsoft.Extensions.Logging;
using Sdk.Modules;
using Semver;

namespace Core.OS.Tests.Modules.Extensions;

public class ModulePackageManifestExtensionsTests
{
    public sealed class GetValidModulePackages
    {
        private readonly ILogger _logger = Substitute.For<ILogger>();

        [Fact]
        public void Should_return_only_enabled_packages_without_dependencies()
        {
            // Arrange
            var enabledModules = new List<string> { "A", "B" };
            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new() { Name = "A", Version = "0.2.0" },
                    new() { Name = "B", Version = "0.3.1"},
                    new() { Name = "C", Version = "0.1.9" }, // disabled                    
                ]
            };

            // Act
            var result = manifest.GetValidModulePackages(enabledModules, [], _logger);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(p => enabledModules.Contains(p.Name));
        }

        [Fact]
        public void Should_exclude_package_when_dependency_cannot_be_found()
        {
            // Arrange
            var dependency = new ModuleDependencyPackage
            {
                Name = "MissingDep",
                Version = "1.0.0"
            };
            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new()
                    {
                        Name = "A",
                        Version = "0.2.0",
                        DependingOn = [dependency]
                    }
                ]
            };

            var moduleIds = new List<string> { "A" };

            // Act
            var result = manifest.GetValidModulePackages(moduleIds, [], _logger);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Should_exclude_package_when_one_of_multiple_dependencies_is_missing()
        {
            // Arrange - a satisfied dependency follows a missing one; the missing result must not be masked.
            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new() { Name = "PresentDep", Version = "1.0.0" },
                    new()
                    {
                        Name = "A",
                        Version = "0.2.0",
                        DependingOn =
                        [
                            new() { Name = "MissingDep", Version = "1.0.0" },
                            new() { Name = "PresentDep", Version = "1.0.0" }
                        ]
                    }
                ]
            };

            var moduleIds = new List<string> { "A", "PresentDep" };

            // Act
            var result = manifest.GetValidModulePackages(moduleIds, [], _logger);

            // Assert
            result.Select(p => p.Name).Should().NotContain("A");
        }

        [Theory]
        [InlineData("1.0.0", "1.0.0")]
        [InlineData("1.0.4", "1.0.0")]
        [InlineData("1.0.0", "1.0.0-ci342343")]
        [InlineData("1.1.0", "1.0.0")]
        [InlineData("1.1.0-ci342343", "1.0.0")]
        public void Should_include_package_when_dependencies_are_supported(string installedVersion, string minDependency)
        {
            // Arrange
            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new() { Name = "Dep", Version = installedVersion },
                    new()
                    {
                        Name = "A",
                        Version = "2.0.0",
                        DependingOn =
                        [
                            new() { Name = "Dep", Version = minDependency }
                        ]
                    }
                ]
            };

            var moduleIds = new List<string> { "A", "Dep" };

            // Act
            var result = manifest.GetValidModulePackages(moduleIds, [], _logger);

            // Assert
            result.Select(p => p.Name).Should().Contain("A");
        }

        [Theory]
        [InlineData("0.9.0", "1.0.0")]
        [InlineData("1.0.0", "1.0.4")]
        [InlineData("1.0.0-ci342343", "1.0.0")]
        [InlineData("1.0.0-ci342343", "1.0.0-ci352343")]
        [InlineData("2.2.0-rc1", "2.2.0")]
        [InlineData("2.1.0", "1.0.0")]
        public void Should_exclude_package_when_dependencies_are_not_supported(string installedVersion, string minDependency)
        {
            // Arrange
            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new() { Name = "Dep", Version = installedVersion },
                    new()
                    {
                        Name = "A",
                        Version = "2.0.0",
                        DependingOn =
                        [
                            new() { Name = "Dep", Version = minDependency }
                        ]
                    }
                ]
            };

            var moduleIds = new List<string> { "A", "Dep" };

            // Act
            var result = manifest.GetValidModulePackages(moduleIds, [], _logger);

            // Assert
            result.Select(p => p.Name).Should().NotContain("A");
        }
    }

    public sealed class UpdateManifestPackageVersions
    {
        [Fact]
        public void Should_update_package_versions_from_given_dictionary()
        {
            // Arrange
            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new() { Name = "A", Version = "0.0.1" },
                    new() { Name = "B", Version = "0.0.1" }
                ]
            };

            var versionMap = new Dictionary<string, SemVersion?>
            {
                ["A"] = SemVersion.Parse("1.2.3"),
                ["B"] = SemVersion.Parse("2.3.4")
            };

            // Act
            var result = manifest.UpdatePackageVersions(versionMap);

            // Assert
            result.Should().HaveCount(2);
            result.First(p => p.Name == "A").Version.Should().Be("1.2.3");
            result.First(p => p.Name == "B").Version.Should().Be("2.3.4");
        }

        [Fact]
        public void Should_update_dependency_versions_if_present_in_map()
        {
            // Arrange
            var dep = new ModuleDependencyPackage { Name = "Dep", Version = "0.0.1" };

            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new()
                    {
                        Name = "A",
                        Version = "0.0.1",
                        DependingOn = [dep]
                    }
                ]
            };

            var versionMap = new Dictionary<string, SemVersion?>
            {
                ["A"] = SemVersion.Parse("9.9.9"),
                ["Dep"] = SemVersion.Parse("5.5.5")
            };

            // Act
            var result = manifest.UpdatePackageVersions(versionMap);

            // Assert
            result.Single(p => p.Name == "A").Version.Should().Be("9.9.9");
            dep.Version.Should().Be("5.5.5");
        }

        [Fact]
        public void Should_not_update_dependency_version_when_dependency_name_not_in_map()
        {
            // Arrange
            var dep = new ModuleDependencyPackage { Name = "Dep", Version = "0.0.1" };

            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new()
                    {
                        Name = "A",
                        Version = "0.0.1",
                        DependingOn = [dep]
                    }
                ]
            };

            // Only the parent package is in the map, not the dependency.
            var versionMap = new Dictionary<string, SemVersion?>
            {
                ["A"] = SemVersion.Parse("9.9.9")
            };

            // Act
            var result = manifest.UpdatePackageVersions(versionMap);

            // Assert
            result.Single(p => p.Name == "A").Version.Should().Be("9.9.9");
            dep.Version.Should().Be("0.0.1");
        }

        [Fact]
        public void Should_ignore_missing_versions_in_map()
        {
            // Arrange
            var manifest = new ModulePackageManifest
            {
                Packages =
                [
                    new() { Name = "A", Version = "current" }
                ]
            };

            var versionMap = new Dictionary<string, SemVersion?>();

            // Act
            var result = manifest.UpdatePackageVersions(versionMap);

            // Assert
            result.First().Version.Should().Be("current");
        }
    }
}
