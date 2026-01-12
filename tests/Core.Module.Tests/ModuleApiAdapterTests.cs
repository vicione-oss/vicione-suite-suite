using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Core.Module.Contracts;
using Core.Module.JFrog;
using Core.Module.Options;
using Core.Tests.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.ArtifactApi;
using Sdk.Modules;
using Sdk.Testing;
using Xunit;

namespace Core.Module.Tests;

public class ModuleApiAdapterTests
{
    private readonly Version _sdkVersion = new(0, 28, 0);
    private readonly string _packageName = "ViciOne.Suite.ClusterManagement";
    private readonly string _packageVersion = "0.32.1";

    private static ServiceProvider CreateServiceProvider()
    {
        var options = Microsoft.Extensions.Options.Options.Create(SystemTestSettings.ModuleApiOptions);

        return new ServiceCollection()
        .AddSingleton<HttpClient>()
        .AddSingleton<IArtifactQueryApi, JFrogArtifactQueryApi>()
        .AddSingleton<IFileSystem>(new FileSystem())
        .AddSingleton<ModuleApiAdapter>()
        .AddSingleton(options)
        .AddHttpClient()
        .BuildServiceProvider();
    }

    [Trait(Traits.Category, Traits.System)]
    public sealed class DownloadAndExtract : ModuleApiAdapterTests
    {
        private const string TestPackageName = "ViciOne.Suite.Ping";
        private const string TestPackageVersion = "0.2.0";

        private static ModuleDependencyPackage GetTestPackage()
        => new() { Name = TestPackageName, Version = TestPackageVersion };


        [Fact]
        public async Task Should_process_single_module_from_http()
        {
            // Arrange
            using var tempDir = new TemporaryDirectory();
            var modulePackage = GetTestPackage();
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var result = await api.DownloadAndExtract(tempDir.Path, modulePackage, CancellationToken.None);

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
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var first = await api.DownloadAndExtract(tempDir.Path, modulePackage, CancellationToken.None);
            var second = await api.DownloadAndExtract(tempDir.Path, modulePackage, CancellationToken.None);

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
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var results = await api.DownloadAndExtract(tempDir.Path, modulePackages, CancellationToken.None);

            // Assert
            Assert.NotEmpty(results);
            results.Where(r => r.Error is not null).Should().BeEmpty();
        }

        [Fact]
        public async Task Should_not_process_anything_on_empty_package_list()
        {
            // Arrange
            using var tempDir = new TemporaryDirectory();
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var results = await api.DownloadAndExtract(tempDir.Path, [], CancellationToken.None);

            // Assert
            Assert.Empty(results);
        }
    }

    public sealed class GetModuleDownloadStreamTest : ModuleApiAdapterTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_return_available_module_stream()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var package = new ModuleDependencyPackage() { Name = _packageName, Version = _packageVersion };
            var api = services.GetRequiredService<ModuleApiAdapter>();
            using var ms = new MemoryStream();

            // Act
            using var moduleStream = await api.GetMetadataDownloadStream(package);
            await moduleStream.CopyToAsync(ms);

            // Assert
            ms.Length.Should().BeGreaterThan(0);
        }
    }

    public sealed class GetModuleMetadata : ModuleApiAdapterTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_return_deserialized_module_metadata()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();
            var apiOptions = services.GetRequiredService<IOptions<ModuleApiOptions>>().Value;
            var fileName = $"{_packageVersion}-{RuntimeInformation.RuntimeIdentifier}_{_sdkVersion}.json";
            var artifact = new ModuleArtifactInfo()
            {
                Name = $"{_packageVersion}-{RuntimeInformation.RuntimeIdentifier}_{_sdkVersion}.json",
                Path = $"modules/{_packageName}",
                Repo = "vicione-suite"
            };

            // Act
            var metadata = await api.GetModuleMetadata(artifact);

            // Assert
            metadata.Should().NotBeNull();
        }
    }

    public sealed class GetMetadataDownloadStreamTest : ModuleApiAdapterTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_return_available_module_metadata_stream()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var package = new ModuleDependencyPackage() { Name = _packageName, Version = _packageVersion };
            var api = services.GetRequiredService<ModuleApiAdapter>();
            using var ms = new MemoryStream();

            // Act
            using var metadataStream = await api.GetMetadataDownloadStream(package);
            await metadataStream.CopyToAsync(ms);

            // Assert
            ms.Length.Should().BeGreaterThan(0);
        }
    }

    public sealed class QueryModuleAssets : ModuleApiAdapterTests
    {
        [Trait(Traits.Category, Traits.System)]
        [Fact]
        public async Task Should_return_available_module_artifcats()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var assets = await api.QueryModuleAssets();

            // Assert
            assets.Should().NotBeEmpty();
        }
    }

    [Trait(Traits.Category, Traits.System)]
    public sealed class QueryModuleMetadataAssets : ModuleApiAdapterTests
    {
        [Fact]
        public async Task Should_return_available_module_artifcats()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var assets = await api.QueryModuleMetadataArtifacts();

            // Assert
            assets.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Should_return_available_module_artifcats_for_sdk_version()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var assets = await api.QueryModuleMetadataArtifacts(sdkVersion: _sdkVersion);

            // Assert
            assets.Should().NotBeEmpty();
            assets.Should().AllSatisfy(k => k.Id.Should().Contain($"_{_sdkVersion.Major}.{_sdkVersion.Minor}."));
        }

        [Fact]
        public async Task Should_return_available_module_assets_without_ci_versions()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var assets = await api.QueryModuleMetadataArtifacts(includePreRelease: false); ;

            // Assert
            assets.Should().NotBeEmpty();
            assets.Should().AllSatisfy(k => k.Id.Should().NotContain("-ci"));
        }
    }

    [Trait(Traits.Category, Traits.System)]
    public sealed class QueryLatestModuleMetadataAsset : ModuleApiAdapterTests
    {
        [Fact]
        public async Task Should_return_latest_module_metadata_asset()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var assets = await api.QueryLatestModuleMetadataArtifact(_sdkVersion, _packageName);

            // Assert
            assets.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_return_null_for_unknown_package()
        {
            // Arrange
            using var services = CreateServiceProvider();
            var api = services.GetRequiredService<ModuleApiAdapter>();

            // Act
            var result = await api.QueryLatestModuleMetadataArtifact(_sdkVersion, "Unknown.Package", false, CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }
    }
}
