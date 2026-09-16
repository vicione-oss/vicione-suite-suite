using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Core.Tests.Tools;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Sdk.Testing;
using Core.Artifacts;
using Core.Artifacts.Extensions;
using Core.Module.Utils;
using Microsoft.Extensions.Logging;

namespace Core.Module.Tests;

public class ModuleArtifactRepositoryTests
{
    private readonly Version _sdkVersion = new(2, 1, 0);
    private readonly string _packageName = "ViciOne.Suite.ClusterManagement";
    private readonly string _packageVersion = "2.1.0";
    private readonly ILogger<ModuleArtifactRepository> _logger = Substitute.For<ILogger<ModuleArtifactRepository>>();

    private static ServiceProvider CreateServiceProvider()
    {
        var optionsProvider = Substitute.For<IArtifactRepositoryOptionsProvider>();
        optionsProvider.GetOptions().Returns(SystemTestSettings.GetArtifactRepositoryOptions());

        return new ServiceCollection()
            .AddSingleton<IFileSystem>(new FileSystem())
            .AddSingleton<ModuleArtifactRepository>()
            .AddSingleton(Substitute.For<ILogger<ModuleArtifactRepository>>())
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

                // Mimic extraction creating the target folder and a payload file so the
                // staged module passes completeness verification before promotion.
                fileSystem.Directory.CreateDirectory(targetPath);
                await fileSystem.File.WriteAllTextAsync(fileSystem.Path.Combine(targetPath, "payload.dll"), "payload");
                Interlocked.Decrement(ref concurrent);
            }

            var repository = new ModuleArtifactRepository(artifactRepository, fileSystem, _logger);
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

    public sealed class InterruptedDownload : ModuleArtifactRepositoryTests
    {
        private static (IArtifactRepository repo, IArtifactQueryBuilder builder) CreateArtifactRepositoryMock()
        {
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

            return (artifactRepository, queryBuilder);
        }

        [Fact]
        public async Task Should_not_promote_partial_module_when_extraction_is_interrupted()
        {
            // Arrange
            var fileSystem = new FileSystem();
            using var tempDir = new TemporaryDirectory();
            var (artifactRepository, _) = CreateArtifactRepositoryMock();
            var package = new ModuleDependencyPackage { Name = "Module.Interrupted", Version = "1.0.0" };
            var targetPath = fileSystem.Path.Combine(tempDir.Path, package.Name, package.Version);

            // Simulate an interrupted extraction: write a partial file into the staging folder, then fail.
            artifactRepository
                .DownloadAndExtract(Arg.Any<IArtifact>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    var stagingPath = call.ArgAt<string>(1);
                    fileSystem.Directory.CreateDirectory(stagingPath);
                    await fileSystem.File.WriteAllTextAsync(fileSystem.Path.Combine(stagingPath, "partial.dll"), "partial");
                    throw new IOException("There is not enough space on the disk.");
                });

            var repository = new ModuleArtifactRepository(artifactRepository, fileSystem, _logger);

            // Act
            var result = await repository.DownloadAndExtract(tempDir.Path, package, TestContext.Current.CancellationToken);

            // Assert
            result.Error.Should().NotBeNull();
            result.Skipped.Should().BeFalse();
            fileSystem.Directory.Exists(targetPath).Should().BeFalse("no partial module directory may be promoted");
        }

        [Fact]
        public async Task Should_reacquire_module_after_interrupted_download()
        {
            // Arrange
            var fileSystem = new FileSystem();
            using var tempDir = new TemporaryDirectory();
            var (artifactRepository, _) = CreateArtifactRepositoryMock();
            var package = new ModuleDependencyPackage { Name = "Module.Reacquire", Version = "1.0.0" };
            var targetPath = fileSystem.Path.Combine(tempDir.Path, package.Name, package.Version);

            var attempts = 0;
            artifactRepository
                .DownloadAndExtract(Arg.Any<IArtifact>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    var stagingPath = call.ArgAt<string>(1);
                    fileSystem.Directory.CreateDirectory(stagingPath);
                    await fileSystem.File.WriteAllTextAsync(fileSystem.Path.Combine(stagingPath, "payload.dll"), "payload");

                    // First attempt is interrupted after writing a partial payload.
                    if (Interlocked.Increment(ref attempts) == 1)
                        throw new IOException("There is not enough space on the disk.");
                });

            var repository = new ModuleArtifactRepository(artifactRepository, fileSystem, _logger);

            // Act
            var first = await repository.DownloadAndExtract(tempDir.Path, package, TestContext.Current.CancellationToken);
            var second = await repository.DownloadAndExtract(tempDir.Path, package, TestContext.Current.CancellationToken);

            // Assert
            first.Error.Should().NotBeNull();
            first.Skipped.Should().BeFalse();

            second.Error.Should().BeNull();
            second.Skipped.Should().BeFalse("a partial module must be re-acquired, not skipped");
            fileSystem.File.Exists(fileSystem.Path.Combine(targetPath, ".ready")).Should().BeTrue();
        }

        [Fact]
        public async Task Should_report_error_and_clean_up_when_archive_is_corrupt()
        {
            // Arrange
            var fileSystem = new FileSystem();
            using var tempDir = new TemporaryDirectory();
            var (artifactRepository, _) = CreateArtifactRepositoryMock();
            var package = new ModuleDependencyPackage { Name = "Module.Corrupt", Version = "1.0.0" };
            var packageFolder = fileSystem.Path.Combine(tempDir.Path, package.Name);
            var targetPath = fileSystem.Path.Combine(packageFolder, package.Version);

            // Simulate a corrupt/truncated archive: extraction surfaces InvalidDataException
            // just like ZipArchive does for an unreadable payload.
            artifactRepository
                .DownloadAndExtract(Arg.Any<IArtifact>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    var stagingPath = call.ArgAt<string>(1);
                    fileSystem.Directory.CreateDirectory(stagingPath);
                    await Task.Yield();
                    throw new InvalidDataException("Central Directory corrupt.");
                });

            var repository = new ModuleArtifactRepository(artifactRepository, fileSystem, _logger);

            // Act
            var result = await repository.DownloadAndExtract(tempDir.Path, package, TestContext.Current.CancellationToken);

            // Assert - the failure is surfaced and nothing is promoted or left behind
            result.Error.Should().BeOfType<InvalidDataException>();
            result.Skipped.Should().BeFalse();
            fileSystem.Directory.Exists(targetPath).Should().BeFalse("a corrupt module must not be promoted");

            if (fileSystem.Directory.Exists(packageFolder))
                fileSystem.Directory.GetDirectories(packageFolder, ".staging-*").Should().BeEmpty("staging must be cleaned up on failure");
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
