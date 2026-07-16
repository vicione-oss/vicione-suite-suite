using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Core.Module;
using Core.Module.Utils;
using Core.OS.Modules;
using Core.OS.Modules.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleArtifactCacheTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IModuleArtifactRepository _moduleRepository = Substitute.For<IModuleArtifactRepository>();
    private readonly IWorkspaceManagement _workspaceManagement = Substitute.For<IWorkspaceManagement>();
    private readonly ILogger<ModuleArtifactCache> _logger = Substitute.For<ILogger<ModuleArtifactCache>>();
    private readonly Version? _sdkVersion = ModuleHelpers.GetSdkAssemblyVersion();

    private ServiceProvider SetupServiceProvider()
    {
        _fileSystem.AddDirectory("/cache");
        _workspaceManagement.GetCacheDirectory(Sdk.Constants.SystemModuleId).Returns(_fileSystem.AllDirectories.Last());

        return new ServiceCollection()
            .AddSingleton(_logger)
            .AddSingleton(_moduleRepository)
            .AddSingleton(_workspaceManagement)
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton<ModuleArtifactCache>()
            .BuildServiceProvider();
    }

    private static async Task<List<IArtifact>> SetupTestModuleMetadataAssets(IModuleArtifactRepository moduleRepository, bool? includePreRelease = null)
    {
        var metadata = await TestFactory.GetEmbeddedModuleMetadata();
        var allEmbeddedArtifacts = await TestFactory.GetEmbeddedModuleArtifacts();
        var metadataArtifacts = allEmbeddedArtifacts
            .Where(k => metadata.Any(m => k.Path.Contains(m.Name, StringComparison.Ordinal)))
            .Where(k => k.Name.Contains("linux-x64", StringComparison.Ordinal)) // because of embedded resource data
            .ToList();

        if (includePreRelease.HasValue && !includePreRelease.Value)
        {
            metadataArtifacts.RemoveAll(k => k.Name.Contains("-ci", StringComparison.Ordinal) || k.Name.Contains("ci-", StringComparison.Ordinal));
        }

        var moduleArtifacts = new List<IArtifact>();

        foreach (var artifact in metadataArtifacts)
        {
            // setup queries to get module artifact by dependency package
            if (ModuleNameVersionRegex.GetModuleVersion(artifact.Name, out var moduleVersion))
            {
                var moduleDependency = new ModuleDependencyPackage
                {
                    Name = artifact.Path.Replace("modules/", "", StringComparison.Ordinal),
                    Version = moduleVersion.ToString()
                };

                moduleRepository.QueryModuleArtifact(moduleDependency, Arg.Any<CancellationToken>()).Returns(artifact);
            }

            var moduleMetadata = metadata.First(m => artifact.Path.Contains(m.Name, StringComparison.Ordinal));
            moduleRepository.GetModuleMetadata(artifact, Arg.Any<CancellationToken>()).Returns(moduleMetadata);
            moduleArtifacts.Add(artifact);
        }

        // The cache always queries with sdkVersion=null and filters by SDK version in memory.
        // The modifiedAfter argument is null on the initial fetch and a watermark on incremental refreshes.
        moduleRepository.QueryModuleMetadataArtifacts(Arg.Any<Version?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([.. moduleArtifacts]);

        // Keep the source set stable so source-change detection does not invalidate the cache between calls.
        moduleRepository.GetSourceKeys().Returns(["source-1"]);

        return [.. moduleArtifacts];
    }

    private static IArtifact CreateMetadataArtifact(string moduleName, string fileName, DateTimeOffset modified, string sourceKey = "source-1")
        => new TestFactory.Artifact
        {
            Repository = "vicione-suite",
            Path = $"modules/{moduleName}",
            Name = fileName,
            Modified = modified,
            SourceKey = sourceKey,
        };

    private static ModuleMetadata CreateMetadata(string name, string version, string minSdkVersion = "1.0.0")
        => new()
        {
            Name = name,
            Version = version,
            MinSuiteSdkVersion = minSdkVersion,
        };

    public class GetAvailableModuleMetadata : ModuleArtifactCacheTests
    {
        [Fact]
        public async Task Should_return_empty_list_if_no_modules_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            _moduleRepository.QueryModuleMetadataArtifacts(_sdkVersion, null, Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var metadata = await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            metadata.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_list_of_packages_including_pre_releases()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            var assets = await SetupTestModuleMetadataAssets(_moduleRepository);

            // Act
            var packages = await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            packages.Should().HaveCount(assets.Count, "One item per asset");
        }

        [Fact]
        public async Task Should_load_items_when_forced()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(true, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            // A forced refresh resets the cache, so both fetches are full queries with modifiedAfter=null.
            await _moduleRepository.Received(2).QueryModuleMetadataArtifacts(_sdkVersion, null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_list_of_packages_having_metadata()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            var metadataList = await TestFactory.GetEmbeddedModuleMetadata();
            var embeddedArtifacts = await TestFactory.GetEmbeddedModuleArtifacts();

            var metadataArtifacts = embeddedArtifacts
                .Where(m => metadataList.Any(k => m.Path.Contains(k.Name, StringComparison.Ordinal)))
                .Where(m => !m.Name.Contains("ci-", StringComparison.Ordinal))
                .DistinctBy(m => m.Name)
                .ToList();

            var firstArtifact = metadataArtifacts.First();
            var firstMetadata = metadataList.FirstOrDefault(k => firstArtifact.Path.Contains(k.Name, StringComparison.Ordinal));
            var moduleArtifacts = metadataArtifacts.Select(k => k);

            _moduleRepository.QueryModuleMetadataArtifacts(_sdkVersion, null, Arg.Any<CancellationToken>())
                .Returns([.. moduleArtifacts]);

            _moduleRepository.GetModuleMetadata(Arg.Is<IArtifact>(k => k!.Name == firstArtifact.Name), Arg.Any<CancellationToken>())
                    .Returns(firstMetadata);

            // Act
            var packages = await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            packages.Should().HaveCount(1, "metadata of first asset");
        }

        [Fact]
        public async Task Should_filter_out_ci_packages_if_pre_releases_excluded()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository, false);

            // Act
            var packages = await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            packages.Should().HaveCount(10, "8xClusterManagement + 2xDataCollectionWizard");
        }
    }

    public class IncrementalRefresh : ModuleArtifactCacheTests
    {
        [Fact]
        public async Task Should_only_download_metadata_for_newly_added_artifacts()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();

            var existing = CreateMetadataArtifact("ModuleA", "1.0.0-linux-x64_1.0.0.json", DateTimeOffset.UtcNow.AddDays(-1));
            var added = CreateMetadataArtifact("ModuleB", "2.0.0-linux-x64_1.0.0.json", DateTimeOffset.UtcNow);

            _moduleRepository.GetSourceKeys().Returns(["source-1"]);
            _moduleRepository.GetModuleMetadata(existing, Arg.Any<CancellationToken>()).Returns(CreateMetadata("ModuleA", "1.0.0"));
            _moduleRepository.GetModuleMetadata(added, Arg.Any<CancellationToken>()).Returns(CreateMetadata("ModuleB", "2.0.0"));

            // First fetch (modifiedAfter == null) returns the existing artifact only.
            _moduleRepository.QueryModuleMetadataArtifacts(_sdkVersion, null, Arg.Any<CancellationToken>()).Returns([existing]);
            // Incremental fetch (modifiedAfter != null) returns the newly added artifact only.
            _moduleRepository.QueryModuleMetadataArtifacts(_sdkVersion, Arg.Is<DateTimeOffset?>(d => d != null), Arg.Any<CancellationToken>()).Returns([added]);

            // Act
            var first = await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);
            await Task.Delay(20, TestContext.Current.CancellationToken); // ensure the cache lifetime elapsed so a delta fetch runs
            var second = await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            first.Should().ContainSingle(m => m.Name == "ModuleA");
            second.Select(m => m.Name).Should().BeEquivalentTo(["ModuleA", "ModuleB"], "the cached item is merged with the newly fetched one");
            // Metadata for each artifact is downloaded exactly once even across refreshes.
            await _moduleRepository.Received(1).GetModuleMetadata(existing, Arg.Any<CancellationToken>());
            await _moduleRepository.Received(1).GetModuleMetadata(added, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_reset_cache_when_source_set_changes()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();

            var artifact = CreateMetadataArtifact("ModuleA", "1.0.0-linux-x64_1.0.0.json", DateTimeOffset.UtcNow);
            _moduleRepository.GetModuleMetadata(artifact, Arg.Any<CancellationToken>()).Returns(CreateMetadata("ModuleA", "1.0.0"));
            _moduleRepository.QueryModuleMetadataArtifacts(_sdkVersion, Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([artifact]);

            // First the cache sees a single source, then the source set changes.
            _moduleRepository.GetSourceKeys().Returns(["source-1"], ["source-1", "source-2"]);

            // Act
            await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);
            await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            // A source-set change must trigger a full fetch (modifiedAfter == null) again.
            await _moduleRepository.Received(2).QueryModuleMetadataArtifacts(_sdkVersion, null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_clear_cache_on_invalidate()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);

            // Act
            await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);
            await cache.Invalidate(TestContext.Current.CancellationToken);
            await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            // After invalidation the next read performs a full fetch again.
            await _moduleRepository.Received(2).QueryModuleMetadataArtifacts(_sdkVersion, null, Arg.Any<CancellationToken>());
        }
    }

    public class Persistence : ModuleArtifactCacheTests
    {
        private string GetExpectedCacheFilePath()
        {
            var cacheDir = _workspaceManagement.GetCacheDirectory(Sdk.Constants.SystemModuleId);
            return _fileSystem.Path.Combine(cacheDir, ModuleArtifactCache.CacheFileName);
        }

        [Fact]
        public async Task Should_persist_cache_to_file_after_fetch()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);

            // Act
            await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(GetExpectedCacheFilePath()).Should().BeTrue();
        }

        [Fact]
        public async Task Should_seed_from_persisted_file_without_full_fetch()
        {
            // Arrange
            var modified = DateTimeOffset.UtcNow;
            var artifact = CreateMetadataArtifact("ModuleA", "1.0.0-linux-x64_1.0.0.json", modified);
            _moduleRepository.GetSourceKeys().Returns(["source-1"]);
            _moduleRepository.GetModuleMetadata(artifact, Arg.Any<CancellationToken>()).Returns(CreateMetadata("ModuleA", "1.0.0"));
            _moduleRepository.QueryModuleMetadataArtifacts(_sdkVersion, Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([artifact]);

            // Populate and persist the cache with a first instance.
            await using (var firstProvider = SetupServiceProvider())
            {
                var firstCache = firstProvider.GetRequiredService<ModuleArtifactCache>();
                await firstCache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);
            }

            _moduleRepository.ClearReceivedCalls();

            // Act - a fresh instance loads the persisted file and only queries the delta (modifiedAfter != null).
            await using var secondProvider = SetupServiceProvider();
            var secondCache = secondProvider.GetRequiredService<ModuleArtifactCache>();
            var packages = await secondCache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            packages.Should().ContainSingle(m => m.Name == "ModuleA");
            await _moduleRepository.DidNotReceive().QueryModuleMetadataArtifacts(_sdkVersion, null, Arg.Any<CancellationToken>());
            await _moduleRepository.Received().QueryModuleMetadataArtifacts(_sdkVersion, Arg.Is<DateTimeOffset?>(d => d != null), Arg.Any<CancellationToken>());
            // No metadata re-download because the loaded entry already covers the artifact.
            await _moduleRepository.DidNotReceive().GetModuleMetadata(Arg.Any<IArtifact>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_discard_persisted_cache_when_sdk_version_changed()
        {
            // Arrange
            var modified = DateTimeOffset.UtcNow;
            var artifact = CreateMetadataArtifact("ModuleA", "1.0.0-linux-x64_1.0.0.json", modified);
            _moduleRepository.GetSourceKeys().Returns(["source-1"]);
            _moduleRepository.GetModuleMetadata(artifact, Arg.Any<CancellationToken>()).Returns(CreateMetadata("ModuleA", "1.0.0"));
            _moduleRepository.QueryModuleMetadataArtifacts(_sdkVersion, Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([artifact]);

            // Populate and persist the cache with a first instance.
            await using (var firstProvider = SetupServiceProvider())
            {
                var firstCache = firstProvider.GetRequiredService<ModuleArtifactCache>();
                await firstCache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);
            }

            // Simulate the persisted file having been written by a different SDK version.
            var cacheFilePath = GetExpectedCacheFilePath();
            var node = JsonNode.Parse(await _fileSystem.File.ReadAllTextAsync(cacheFilePath, TestContext.Current.CancellationToken))!;
            node["sdkVersion"] = "0.0.0";
            await _fileSystem.File.WriteAllTextAsync(cacheFilePath, node.ToJsonString(), TestContext.Current.CancellationToken);

            _moduleRepository.ClearReceivedCalls();

            // Act - a fresh instance must discard the stale snapshot and perform a full fetch again.
            await using var secondProvider = SetupServiceProvider();
            var secondCache = secondProvider.GetRequiredService<ModuleArtifactCache>();
            var packages = await secondCache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);

            // Assert
            packages.Should().ContainSingle(m => m.Name == "ModuleA");
            await _moduleRepository.Received().QueryModuleMetadataArtifacts(_sdkVersion, null, Arg.Any<CancellationToken>());
            // The metadata must be re-downloaded because the persisted entries were discarded.
            await _moduleRepository.Received(1).GetModuleMetadata(artifact, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_delete_persisted_file_on_invalidate()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            var cacheFilePath = GetExpectedCacheFilePath();
            await SetupTestModuleMetadataAssets(_moduleRepository);
            await cache.GetAvailableModuleMetadata(false, TestContext.Current.CancellationToken);
            _fileSystem.File.Exists(cacheFilePath).Should().BeTrue();

            // Act
            await cache.Invalidate(TestContext.Current.CancellationToken);

            // Assert
            _fileSystem.File.Exists(cacheFilePath).Should().BeFalse();
        }
    }
}
