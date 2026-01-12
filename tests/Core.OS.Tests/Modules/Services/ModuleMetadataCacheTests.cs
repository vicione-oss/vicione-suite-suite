using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Core.Module;
using Core.Module.Contracts;
using Core.Module.Options;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.OS.Modules.Factories;
using Core.OS.Modules.Services;
using Core.OS.Tests.Modules.Consumers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.ArtifactApi;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleMetadataCacheTests
{
    private readonly ILogger<ModuleMetadataCache> _logger = Substitute.For<ILogger<ModuleMetadataCache>>();
    private readonly IModuleApiAdapter _moduleApi = Substitute.For<IModuleApiAdapter>();
    private readonly IModuleHost _moduleManager = Substitute.For<IModuleHost>();
    private readonly IModuleOptionsStore _optionsStore = Substitute.For<IModuleOptionsStore>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly IOptions<ModuleApiOptions> _options = Substitute.For<IOptions<ModuleApiOptions>>();
    private readonly ModuleApiOptions _apiOptions = new() { PackageCacheLifetimeMs = 800 };
    private readonly IServiceProvider _serviceProvider;

    public ModuleMetadataCacheTests()
    {
        _options.Value.Returns(_apiOptions);

        _optionsStore.LoadJsonDictionary(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Dictionary<string, string?>()));

        _serviceProvider = new ServiceCollection()
            .AddSingleton(_logger)
            .AddSingleton(_moduleManager)
            .AddSingleton(_moduleApi)
            .AddSingleton(_options)
            .AddSingleton(_optionsStore)
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton<ModuleMetadataCache>()
            .BuildServiceProvider();
    }

    private static async Task<List<IArtifactItem>> SetupTestModuleMetadataAssets(IModuleApiAdapter moduleApi, bool? includePreRelease = null)
    {
        var metadata = await TestFactory.GetEmbeddedModuleMetadata();
        var allEmbeddedArtifacts = await TestFactory.GetEmbeddedQueryModuleMetadataArtifacts();
        var metadataArtifacts = allEmbeddedArtifacts
            .Where(k => metadata.Any(m => k.Path.Contains(m.Name, StringComparison.Ordinal)))
            .Where(k => k.Name.Contains("linux-x64", StringComparison.Ordinal)) // because of embedded resource data
            .ToList();

        if (includePreRelease.HasValue && !includePreRelease.Value)
        {
            metadataArtifacts.RemoveAll(k => k.Name.Contains("-ci", StringComparison.Ordinal) || k.Name.Contains("ci-", StringComparison.Ordinal));
        }

        var moduleArtifacts = new List<ModuleArtifactInfo>();

        foreach (var artifact in metadataArtifacts)
        {
            var moduleMetadata = metadata.First(m => artifact.Path.Contains(m.Name, StringComparison.Ordinal));
            var moduleArtifact = TestFactory.CreateModuleArtifactInfo(artifact);

            moduleApi.GetModuleMetadata(moduleArtifact, Arg.Any<CancellationToken>()).Returns(moduleMetadata);
            moduleArtifacts.Add(moduleArtifact);
        }

        moduleApi.QueryModuleMetadataArtifacts(Arg.Any<Version>(), includePreRelease ?? Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([.. moduleArtifacts]);

        return [.. moduleArtifacts];
    }

    private void SetupMetadataFiles(SuiteDependencyContext suiteContext)
    {
        var assembly = Assembly.GetAssembly(typeof(GetModuleMetadataBundlesConsumerTests));
        var metadataResource = $"{TestFactory.ModuleResourceNamespace}.{TestFactory.ClusterManagementMetadataResource}";

        foreach (var module in suiteContext.Modules)
        {
            var metadataPath = _fileSystem.Path.Combine(module.AssemblyFolder, ModuleMetadataCache.MetadataFileName);

            _fileSystem.AddDirectory(module.AssemblyFolder);
            _fileSystem.AddFileFromEmbeddedResource(metadataPath, assembly, metadataResource);
        }
    }

    public class GetAvailableModuleMetadata : ModuleMetadataCacheTests
    {
        [Fact]
        public async Task Should_return_empty_list_if_no_modules_available()
        {
            // Arrange
            const bool includePreReleases = false;
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            _moduleApi.QueryModuleMetadataArtifacts(null, includePreReleases, Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var metadata = await cache.GetAvailableModuleMetadata(null, includePreReleases, false, CancellationToken.None);

            // Assert
            metadata.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_list_of_packages_including_pre_releases()
        {
            // Arrange
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            var assets = await SetupTestModuleMetadataAssets(_moduleApi);

            // Act
            var packages = await cache.GetAvailableModuleMetadata(null, true, false, CancellationToken.None);

            // Assert
            packages.Should().HaveCount(assets.Count, "One item per asset");
        }

        [Fact]
        public async Task Should_use_cached_items_within_cache_lifetime()
        {
            // Arrange
            const bool includePreReleases = false;
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            _moduleApi.QueryModuleMetadataArtifacts(null, false, Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, includePreReleases, false, CancellationToken.None);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, includePreReleases, false, CancellationToken.None);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleApi.Received(1).QueryModuleMetadataArtifacts(null, includePreReleases, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_if_cache_lifetime_exceeded()
        {
            // Arrange
            const bool includePreReleases = false;
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            await SetupTestModuleMetadataAssets(_moduleApi);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, includePreReleases, false, CancellationToken.None);
            await Task.Delay((int)_apiOptions.PackageCacheLifetimeMs + 500);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, includePreReleases, false, CancellationToken.None);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleApi.Received(2).QueryModuleMetadataArtifacts(null, includePreReleases, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_when_forced()
        {
            // Arrange
            const bool includePreReleases = false;
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            await SetupTestModuleMetadataAssets(_moduleApi);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, includePreReleases, false, CancellationToken.None);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, includePreReleases, true, CancellationToken.None);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleApi.Received(2).QueryModuleMetadataArtifacts(null, includePreReleases, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_when_pre_release_flag_changed()
        {
            // Arrange
            const bool includePreReleases = false;
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            await SetupTestModuleMetadataAssets(_moduleApi);
            var version = new Version("1.0.0");

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, includePreReleases, false, CancellationToken.None);
            var metadata2 = await cache.GetAvailableModuleMetadata(version, includePreReleases, false, CancellationToken.None);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleApi.Received(1).QueryModuleMetadataArtifacts(null, includePreReleases, Arg.Any<CancellationToken>());
            await _moduleApi.Received(1).QueryModuleMetadataArtifacts(version, includePreReleases, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_list_of_packages_having_metadata()
        {
            // Arrange
            const bool includePreReleases = false;
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            var metadataList = await TestFactory.GetEmbeddedModuleMetadata();
            var embeddedArtifacts = await TestFactory.GetEmbeddedQueryModuleMetadataArtifacts();

            var metadataArtifacts = embeddedArtifacts
                .Where(m => metadataList.Any(k => m.Path.Contains(k.Name, StringComparison.Ordinal)))
                .Where(m => !m.Name.Contains("ci-", StringComparison.Ordinal))
                .DistinctBy(m => m.Name);

            var firstArtifact = TestFactory.CreateModuleArtifactInfo(metadataArtifacts.First());
            var firstMetadata = metadataList.FirstOrDefault(k => firstArtifact.Path.Contains(k.Name, StringComparison.Ordinal));

            var moduleArtifacts = metadataArtifacts.Select(TestFactory.CreateModuleArtifactInfo);

            _moduleApi.QueryModuleMetadataArtifacts(null, includePreReleases, Arg.Any<CancellationToken>())
                .Returns([.. moduleArtifacts]);

            _moduleApi.GetModuleMetadata(Arg.Is<ModuleArtifactInfo>(k => k.Name == firstArtifact.Name), Arg.Any<CancellationToken>())
                    .Returns(firstMetadata);

            // Act
            var packages = await cache.GetAvailableModuleMetadata(null, includePreReleases, false, CancellationToken.None);

            // Assert
            packages.Should().HaveCount(1, "metadata of first asset");
        }

        [Fact]
        public async Task Should_filter_out_ci_packages_if_pre_releases_excluded()
        {
            // Arrange
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            await SetupTestModuleMetadataAssets(_moduleApi, false);

            // Act
            var packages = await cache.GetAvailableModuleMetadata(null, false, false, CancellationToken.None);

            // Assert
            packages.Should().HaveCount(7, "1xClusterManagement + 6xDataCollectionWizard");
        }
    }

    public class GetInstalledModuleMetadata() : ModuleMetadataCacheTests
    {
        [Fact]
        public async Task Should_return_infos_for_debug_modules()
        {
            // Arrange
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            var coreContext = TestFactory.CreateBackendModuleContext(typeof(GetModuleMetadataBundlesConsumer));
            var debugContext = TestFactory.CreateBackendModuleContext(typeof(TestBackendModule), true);

            var suiteContext = new SuiteDependencyContext(coreContext, [debugContext], []);
            SetupMetadataFiles(suiteContext);

            _moduleManager.GetContext().Returns(suiteContext);

            // Act        
            var result = await cache.GetInstalledModuleMetadata();

            // Assert       
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_provide_installed_modules()
        {
            // Arrange
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            var suiteContext = TestFactory.CreateSuiteContext();

            SetupMetadataFiles(suiteContext);

            var bundle = ModuleMetadataBundleFactory.CreateFallbackBundle(_fileSystem, suiteContext.Modules.First());

            _moduleManager.GetManifestModules().Returns([bundle]);

            // Act        
            var result = await cache.GetInstalledModuleMetadata();

            // Assert       
            result.Should().HaveCount(1, "ClusterManagement");
        }

        [Fact]
        public async Task Should_create_fallback_if_metadata_is_not_available()
        {
            // Arrange
            var cache = _serviceProvider.GetRequiredService<ModuleMetadataCache>();
            var suiteContext = TestFactory.CreateSuiteContext();
            var bundle = ModuleMetadataBundleFactory.CreateFallbackBundle(_fileSystem, suiteContext.Modules.First());

            _moduleManager.GetManifestModules().Returns([bundle]);

            // Act        
            var result = await cache.GetInstalledModuleMetadata();

            // Assert       
            result.Should().ContainSingle(k => k.Metadata.Description == "Generated metadata");
        }
    }
}
