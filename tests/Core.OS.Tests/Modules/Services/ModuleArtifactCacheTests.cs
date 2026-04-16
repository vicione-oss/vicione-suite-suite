using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AwesomeAssertions;
using Core.Module;
using Core.Module.Options;
using Core.Module.Utils;
using Core.OS.Modules.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Artifacts;
using Sdk.Modules;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleArtifactCacheTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IModuleArtifactRepository _moduleRepository = Substitute.For<IModuleArtifactRepository>();
    private readonly ArtifactRepositoryOptions _apiOptions = new() { PackageCacheLifetimeMs = 800 };
    private readonly ILogger<ModuleArtifactCache> _logger = Substitute.For<ILogger<ModuleArtifactCache>>();

    private ServiceProvider SetupServiceProvider()
    {
        var options = Substitute.For<IOptions<ArtifactRepositoryOptions>>();
        options.Value.Returns(_apiOptions);

        return new ServiceCollection()
            .AddSingleton(_logger)
            .AddSingleton(_moduleRepository)
            .AddSingleton(options)
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
            if (ModuleNameVersionRegex.GetVersions(artifact.Name, out var parsed, out var ciVersion))
            {
                var moduleDependency = new ModuleDependencyPackage
                {
                    Name = artifact.Path.Replace("modules/", "", StringComparison.Ordinal),
                    Version = string.IsNullOrEmpty(ciVersion) ? parsed : $"{parsed}-{ciVersion}"
                };

                moduleRepository.QueryModuleArtifact(moduleDependency, Arg.Any<CancellationToken>()).Returns(artifact);
            }

            var moduleMetadata = metadata.First(m => artifact.Path.Contains(m.Name, StringComparison.Ordinal));
            moduleRepository.GetModuleMetadata(artifact, Arg.Any<CancellationToken>()).Returns(moduleMetadata);
            moduleArtifacts.Add(artifact);
        }

        moduleRepository.QueryModuleMetadataArtifacts(Arg.Any<Version>(), null, Arg.Any<CancellationToken>()).Returns([.. moduleArtifacts]);

        return [.. moduleArtifacts];
    }

    public class GetAvailableModuleMetadata : ModuleArtifactCacheTests
    {
        [Fact]
        public async Task Should_return_empty_list_if_no_modules_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            _moduleRepository.QueryModuleMetadataArtifacts(null, null,Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var metadata = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

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
            var packages = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

            // Assert
            packages.Should().HaveCount(assets.Count, "One item per asset");
        }

        [Fact]
        public async Task Should_use_cached_items_within_cache_lifetime()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            _moduleRepository.QueryModuleMetadataArtifacts(null, null,Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleRepository.Received(1).QueryModuleMetadataArtifacts(null, null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_if_cache_lifetime_exceeded()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);
            await Task.Delay((int)_apiOptions.PackageCacheLifetimeMs + 200, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleRepository.Received(2).QueryModuleMetadataArtifacts(null, null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_when_forced()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, true, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleRepository.Received(2).QueryModuleMetadataArtifacts(null, null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_when_pre_release_flag_changed()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleArtifactCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);
            var version = new Version("1.0.0");

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(version, false, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleRepository.Received(1).QueryModuleMetadataArtifacts(null, null, Arg.Any<CancellationToken>());
            await _moduleRepository.Received(1).QueryModuleMetadataArtifacts(version, null,Arg.Any<CancellationToken>());
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

            _moduleRepository.QueryModuleMetadataArtifacts(null, null, Arg.Any<CancellationToken>())
                .Returns([.. moduleArtifacts]);

            _moduleRepository.GetModuleMetadata(Arg.Is<IArtifact>(k => k.Name == firstArtifact.Name), Arg.Any<CancellationToken>())
                    .Returns(firstMetadata);

            // Act
            var packages = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

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
            var packages = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

            // Assert
            packages.Should().HaveCount(10, "8xClusterManagement + 2xDataCollectionWizard");
        }
    }
}
