using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Core.Module.JFrog;
using Core.Tests.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Sdk.Testing;
using Xunit;

namespace Core.Module.Tests;

public class ModuleArtifactRepositoryTests
{
    private readonly Version _sdkVersion = new(1, 0, 0);
    private readonly string _packageName = "ViciOne.Suite.ClusterManagement";
    private readonly string _packageVersion = "1.2.7";

    private static ServiceProvider CreateServiceProvider()
    {
        var options = Microsoft.Extensions.Options.Options.Create(SystemTestSettings.ArtifactApiOptions);

        return new ServiceCollection()
            .AddSingleton<IArtifactRepository, JFrogArtifactRepository>()
            .AddSingleton<IFileSystem>(new FileSystem())
            .AddSingleton<ModuleArtifactRepository>()
            .AddSingleton(options)
            .AddHttpClient()
            .BuildServiceProvider();
    }

    [Trait(Traits.Category, Traits.System)]
    public sealed class DownloadAndExtract : ModuleArtifactRepositoryTests
    {
        private const string TestPackageName = "ViciOne.Suite.Ping";
        private const string TestPackageVersion = "0.11.0";

        private static ModuleDependencyPackage GetTestPackage()
            => new() { Name = TestPackageName, Version = TestPackageVersion };


        [Fact]
        public async Task Should_process_single_module_from_http()
        {
            // Arrange
            using var tempDir = new TemporaryDirectory();
            var modulePackage = GetTestPackage();
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var result = await repository.DownloadAndExtract(tempDir.Path, modulePackage, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result.Error);
            Assert.False(result.Skipped);
        }

        [Fact]
        public async Task Should_skip_existing_module_if_overwrite_not_set()
        {
            // Arrange
            using var tempDir = new TemporaryDirectory();
            var modulePackage = GetTestPackage();
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var first = await repository.DownloadAndExtract(tempDir.Path, modulePackage, TestContext.Current.CancellationToken);
            var second = await repository.DownloadAndExtract(tempDir.Path, modulePackage, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(first.Error);
            Assert.False(first.Skipped);
            Assert.Null(second.Error);
            Assert.True(second.Skipped);
        }

        [Fact]
        public async Task Should_process_multiple_modules_from_http()
        {
            // Arrange
            using var tempDir = new TemporaryDirectory();
            var modulePackages = new List<ModuleDependencyPackage> { GetTestPackage() };
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var results = await repository.DownloadAndExtract(tempDir.Path, modulePackages, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(results);
            results.Where(r => r.Error is not null).Should().BeEmpty();
        }

        [Fact]
        public async Task Should_not_process_anything_on_empty_package_list()
        {
            // Arrange
            using var tempDir = new TemporaryDirectory();
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var results = await repository.DownloadAndExtract(tempDir.Path, [], TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }
    }

    public sealed class GetModuleDownloadStreamTest : ModuleArtifactRepositoryTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_return_available_module_stream()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var package = new ModuleDependencyPackage() { Name = _packageName, Version = _packageVersion };
            var repository = services.GetRequiredService<ModuleArtifactRepository>();
            using var ms = new MemoryStream();

            // Act
            await using var moduleStream = await repository.GetMetadataDownloadStream(package, TestContext.Current.CancellationToken);
            await moduleStream.CopyToAsync(ms, TestContext.Current.CancellationToken);

            // Assert
            ms.Length.Should().BeGreaterThan(0);
        }
    }

    public sealed class GetModuleMetadata : ModuleArtifactRepositoryTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_return_deserialized_module_metadata()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();
            var artifact = Substitute.For<IArtifact>();
            artifact.Name.Returns($"{_packageVersion}-{RuntimeInformation.RuntimeIdentifier}_{_sdkVersion}.json");
            artifact.Path.Returns($"modules/{_packageName}");
            artifact.Repository.Returns("vicione-suite");

            // Act
            var metadata = await repository.GetModuleMetadata(artifact, TestContext.Current.CancellationToken);

            // Assert
            metadata.Should().NotBeNull();
        }
    }

    public sealed class GetMetadataDownloadStreamTest : ModuleArtifactRepositoryTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_return_available_module_metadata_stream()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var package = new ModuleDependencyPackage() { Name = _packageName, Version = _packageVersion };
            var repository = services.GetRequiredService<ModuleArtifactRepository>();
            using var ms = new MemoryStream();

            // Act
            await using var metadataStream = await repository.GetMetadataDownloadStream(package, TestContext.Current.CancellationToken);
            await metadataStream.CopyToAsync(ms, TestContext.Current.CancellationToken);

            // Assert
            ms.Length.Should().BeGreaterThan(0);
        }
    }

    public sealed class QueryModuleAssets : ModuleArtifactRepositoryTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_return_available_module_artifacts()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var assets = await repository.QueryModuleArtifacts(TestContext.Current.CancellationToken);

            // Assert
            assets.Should().NotBeEmpty();
        }
    }

    [Trait(Traits.Category, Traits.System)]
    public sealed class QueryModuleMetadataAssets : ModuleArtifactRepositoryTests
    {
        [Fact]
        public async Task Should_return_available_module_artifacts()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var assets = await repository.QueryModuleMetadataArtifacts(cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            assets.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_return_available_module_artifacts_for_sdk_version()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var assets = await repository.QueryModuleMetadataArtifacts(_sdkVersion, TestContext.Current.CancellationToken);

            // Assert
            assets.Should().NotBeEmpty();
            assets.Should().AllSatisfy(k => k.Name.Should().Contain($"_{_sdkVersion.Major}.{_sdkVersion.Minor}."));
        }
    }

    [Trait(Traits.Category, Traits.System)]
    public sealed class QueryLatestModuleMetadataAsset : ModuleArtifactRepositoryTests
    {
        [Fact]
        public async Task Should_return_latest_module_metadata_asset()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var assets = await repository.QueryLatestModuleMetadataArtifact(_sdkVersion, _packageName, TestContext.Current.CancellationToken);

            // Assert
            assets.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_return_null_for_unknown_package()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var result = await repository.QueryLatestModuleMetadataArtifact(_sdkVersion, "Unknown.Package", TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }
    }
}
