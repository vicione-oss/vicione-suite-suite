using System.IO.Abstractions;
using Core.Module.Extensions;
using Core.Module.Options;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace Core.Module.Tests.Extensions;

public class ModuleLoaderOptionsExtensionsTests
{
    [Theory]
    [InlineData("/vo-suite/src/Core.OS/bin/Debug/net9.0/suite-modules/ViciOne.Suite.News/0.21.0/ViciOne.Suite.News.Backend.dll")]
    [InlineData("/vo-suite-test/suite-modules/ViciOne.Suite.Test/0.24.0/ViciOne.Suite.Test.Backend.dll")]
    public void Should_return_true_for_deployed_modules(string path)
    {
        // Arrange
        var loaderOptions = new ModuleLoaderOptions();

        // Act
        var canBeIncluded = loaderOptions.AllowInclude(path);

        // Assert
        canBeIncluded.Should().BeTrue();
    }

    [Theory]
    [InlineData("/vo-module-cluster-mgmt/src/ClusterManagement.Backend/bin/Debug/net9.0/ViciOne.ClusterManagement.Backend.dll")]
    [InlineData("/vo-suite-module-cluster-mgmt/src/ClusterManagement.Backend/bin/Debug/net9.0/ViciOne.ClusterManagement.Backend.dll")]
    [InlineData("/vo-suite-modules-so/src/ClusterManagement.Client/bin/Debug/net9.0/ViciOne.ClusterManagement.Client.dll")]
    [InlineData("/vo-suite/src/Core.OS/bin/Debug/net9.0/suite-modules/ViciOne.Suite.News/0.21.0/ViciOne.Suite.News.Backend.dll")]
    public void Should_return_true_for_allowed_debug_module_paths(string path)
    {
        // Arrange
        var loaderOptions = new ModuleLoaderOptions();

        // Act
        var canBeIncluded = loaderOptions.AllowInclude(path, true);

        // Assert
        canBeIncluded.Should().BeTrue();
    }

    [Theory]
    [InlineData("/suite-cluster-mgmt/src/ClusterManagement.Client/bin/ViciOne.ClusterManagement.Client.json")]
    [InlineData("/suite-cluster-mgmt/src/ClusterManagement.Client/bin/AppData/ViciOne.ClusterManagement.Client.dll")]
    [InlineData("/suite-cluster-mgmt/src/ClusterManagement.Client/bin/Benchmarks/ViciOne.ClusterManagement.Client.dll")]
    [InlineData("/opt/vo-suite-module-cluster-mgmt/src/ClusterManagement.Backend/bin/Debug/net9.0/AppData/ViciOne.ClusterManagement.Client.dll")]
    public void Should_return_false_for_bad_deploy_paths(string path)
    {
        // Arrange
        var loaderOptions = new ModuleLoaderOptions();

        // Act
        var canBeIncluded = loaderOptions.AllowInclude(path);

        // Assert
        canBeIncluded.Should().BeFalse();
    }

    [Theory]
    [InlineData("/vo-module-cluster-mgmt/src/ClusterManagement.Backend/bin/Release/net9.0/ViciOne.ClusterManagement.Backend.dll")]
    [InlineData("/suite-cluster-mgmt/src/ClusterManagement.Client/bin/ViciOne.ClusterManagement.Client.dll")]
    [InlineData("/vo-suite-module-cluster-mgmt/src/ClusterManagement.Backend/bin/Debug/net9.0/AppData/ViciOne.ClusterManagement.Client.dll")]
    [InlineData("/vo-suite-module-cluster-mgmt/src/ClusterManagement.Backend/bin/Debug/net9.0/AppData/Test/ViciOne.ClusterManagement.Client.dll")]
    [InlineData("/vo-suite-modules-so/src/ClusterManagement.Client/bin/Debug/net9.0/AppData/Module/ViciOne.ClusterManagement.Client.dll")]
    [InlineData("/vo-suite-mod/src/ClusterManagement.Client/bin/Debug/net9.0/Benchmarks/Module/ViciOne.ClusterManagement.Client.dll")]
    [InlineData("/vo-suite-mod/src/ClusterManagement.Client/bin/Debug/net9.0/Benchmarks/ViciOne.ClusterManagement.Client.dll")]
    public void Should_return_false_for_bad_debug_module_paths(string path)
    {
        // Arrange
        var loaderOptions = new ModuleLoaderOptions();

        // Act
        var canBeIncluded = loaderOptions.AllowInclude(path, true);

        // Assert
        canBeIncluded.Should().BeFalse();
    }

    [Theory]
    [InlineData("/repo/suite-modules/path/Debug/ViciOne.ClusterManagement.Backend.dll")]
    [InlineData("/opt/suite-modules/ViciOne.ClusterManagement/Debug/ViciOne.ClusterManagement.Backend.dll")]
    public void Should_return_true_for_debug_module_paths_not_under_deployed_modules_path(string path)
    {
        // Arrange
        var fileSystem = Substitute.For<IFileSystem>();
        var loaderOptions = new ModuleLoaderOptions
        {
            ModulesPath = "/opt/app/suite-modules"
        };

        fileSystem.Path.Combine(Arg.Any<string>(), Arg.Any<string>()).Returns(loaderOptions.ModulesPath);

        // Act
        var canBeIncluded = loaderOptions.IsValidModuleLocation(fileSystem, path);

        // Assert
        canBeIncluded.Should().BeTrue();
    }

    [Theory]
    [InlineData("/opt/app/suite-modules/Debug/ViciOne.ClusterManagement.Backend.dll")]
    [InlineData("/opt/app/suite-modules/ViciOne.ClusterManagement/Debug/ViciOne.ClusterManagement.Backend.dll")]
    public void Should_throw_for_debug_module_paths_under_deployed_modules_path(string path)
    {
        // Arrange
        var fileSystem = Substitute.For<IFileSystem>();
        var loaderOptions = new ModuleLoaderOptions
        {
            ModulesPath = "/opt/app/suite-modules"
        };

        fileSystem.Path.Combine(Arg.Any<string>(), Arg.Any<string>()).Returns(loaderOptions.ModulesPath);

        // Act
        var action = () => _ = loaderOptions.IsValidModuleLocation(fileSystem, path);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }
}
