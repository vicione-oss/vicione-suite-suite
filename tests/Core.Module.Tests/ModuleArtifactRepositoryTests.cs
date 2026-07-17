using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Core.Tests.Tools;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Sdk.Testing;
using Xunit;
using Core.Artifacts;
using Core.Artifacts.Extensions;
using Core.Module.Utils;

namespace Core.Module.Tests;

public class ModuleArtifactRepositoryTests
{
    private readonly Version _sdkVersion = new(2, 1, 0);
    private readonly string _packageName = "ViciOne.Suite.ClusterManagement";
    private readonly string _packageVersion = "2.1.0";

    private static ServiceProvider CreateServiceProvider()
    {
        var optionsProvider = Substitute.For<IArtifactRepositoryOptionsProvider>();
        optionsProvider.GetOptions().Returns(SystemTestSettings.GetArtifactRepositoryOptions());

        return new ServiceCollection()
            .AddSingleton<IFileSystem>(new FileSystem())
            .AddSingleton<ModuleArtifactRepository>()
            .AddSingleton(optionsProvider)
            .AddHttpClient()
            .AddArtifactRepository(s => optionsProvider)
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

    public sealed class BulkDownloadConcurrency : ModuleArtifactRepositoryTests
    {
        [Fact]
        public async Task Should_cap_concurrent_module_downloads()
        {
            // Arrange
            var fileSystem = new FileSystem();
            using var tempDir = new TemporaryDirectory();

            var artifactRepository = Substitute.For<IArtifactRepository>();

            var queryBuilder = Substitute.For<IArtifactQueryBuilder>();
            queryBuilder.AndPathMatches(Arg.Any<string>()).Returns(queryBuilder);
            queryBuilder.AndNameMatches(Arg.Any<string>()).Returns(queryBuilder);
            queryBuilder.OrderByDescending(Arg.Any<string[]>()).Returns(queryBuilder);
            queryBuilder.Build().Returns("query");
            artifactRepository.CreateQueryBuilder().Returns(queryBuilder);

            var artifact = Substitute.For<IArtifact>();
            var queryResult = Substitute.For<IArtifactQueryResult>();
            queryResult.Artifacts.Returns([artifact]);
            artifactRepository.Query(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(queryResult);
            artifactRepository.Download(Arg.Any<IArtifact>(), Arg.Any<CancellationToken>())
                .Returns(_ => new MemoryStream("{}"u8.ToArray()));

            var concurrent = 0;
            var maxObserved = 0;

            artifactRepository
                .DownloadAndExtract(Arg.Any<IArtifact>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => TrackConcurrency(call.ArgAt<string>(1)));

            async Task TrackConcurrency(string targetPath)
            {
                var current = Interlocked.Increment(ref concurrent);
                InterlockedMax(ref maxObserved, current);

                // Hold the "download" open long enough for overlap to be observable.
                await Task.Delay(50);

                // Mimic extraction creating the target folder so metadata can be written afterwards.
                fileSystem.Directory.CreateDirectory(targetPath);
                Interlocked.Decrement(ref concurrent);
            }

            var repository = new ModuleArtifactRepository(artifactRepository, fileSystem);
            var packages = Enumerable.Range(0, 12)
                .Select(i => new ModuleDependencyPackage { Name = $"Module.{i}", Version = "1.0.0" })
                .ToArray();

            // Act
            var results = await repository.DownloadAndExtract(tempDir.Path, packages, TestContext.Current.CancellationToken);

            // Assert
            results.Should().HaveCount(packages.Length);
            var errors = results.Where(r => r.Error is not null).Select(r => r.Error!.Message).ToArray();
            errors.Should().BeEmpty("no download should fail but got: " + string.Join(" | ", errors));

            // Concurrency must be bounded: fewer in-flight downloads than packages and within the cap.
            maxObserved.Should().BeGreaterThan(0);
            maxObserved.Should().BeLessThan(packages.Length);
            maxObserved.Should().BeLessThanOrEqualTo(5);
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int initial;
            do
            {
                initial = Volatile.Read(ref target);
                if (value <= initial)
                    return;
            }
            while (Interlocked.CompareExchange(ref target, value, initial) != initial);
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
            var assets = await repository.QueryModuleMetadataArtifacts(_sdkVersion, null, TestContext.Current.CancellationToken);

            // Assert
            assets.Should().NotBeEmpty();
            assets.Should().AllSatisfy(k => k.Name.Should().Contain($"_{_sdkVersion.Major}."));
        }

        [Fact]
        public async Task Should_return_available_module_artifacts_modified_after()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();
            var datetime = DateTime.UtcNow.Subtract(TimeSpan.FromDays(2));

            // Act
            var assets = await repository.QueryModuleMetadataArtifacts(null, datetime, TestContext.Current.CancellationToken);

            // Assert
            assets.Should().NotBeEmpty();
            assets.Should().AllSatisfy(k => k.Modified.Should().BeAfter(datetime));
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
            var asset = await repository.QueryLatestModuleMetadataArtifact(_sdkVersion, _packageName, null, null, TestContext.Current.CancellationToken);

            // Assert
            asset.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_return_latest_module_metadata_asset_with_fixed_major_version()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();
            var major = 2;

            // Act
            var asset = await repository.QueryLatestModuleMetadataArtifact(_sdkVersion, _packageName, major, null, TestContext.Current.CancellationToken);

            // Assert
            asset.Should().NotBeNull();
            ModuleNameVersionRegex.GetModuleVersion(asset.Name, out var moduleVersion).Should().BeTrue();
            moduleVersion!.Major.Should().Be(major);
        }

        [Fact]
        public async Task Should_return_latest_module_metadata_asset_with_fixed_major_and_minor_version()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();
            var major = 2;
            var minor = 1;

            // Act
            var asset = await repository.QueryLatestModuleMetadataArtifact(_sdkVersion, _packageName, major, minor, TestContext.Current.CancellationToken);

            // Assert
            asset.Should().NotBeNull();
            ModuleNameVersionRegex.GetModuleVersion(asset.Name, out var moduleVersion).Should().BeTrue();
            moduleVersion!.Major.Should().Be(major);
            moduleVersion!.Minor.Should().Be(minor);
        }

        [Fact]
        public async Task Should_return_null_if_no_latest_version_is_available()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();
            var major = 200;

            // Act
            var asset = await repository.QueryLatestModuleMetadataArtifact(_sdkVersion, _packageName, major, 1, TestContext.Current.CancellationToken);

            // Assert
            asset.Should().BeNull();
        }

        [Fact]
        public async Task Should_return_null_for_unknown_package()
        {
            // Arrange
            await using var services = CreateServiceProvider();
            var repository = services.GetRequiredService<ModuleArtifactRepository>();

            // Act
            var result = await repository.QueryLatestModuleMetadataArtifact(_sdkVersion, "Unknown.Package", null, null, TestContext.Current.CancellationToken);

            // Assert
            result.Should().BeNull();
        }
    }
}
