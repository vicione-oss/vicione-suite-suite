using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Core.Module;
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
using Sdk.Backend.Artifacts;
using TestModule.Backend;
using Xunit;
using Core.Module.Utils;
using Sdk.Modules;

namespace Core.OS.Tests.Modules.Services;

public class ModuleMetadataCacheTests
{
    private readonly ILogger<ModuleMetadataCache> _logger = Substitute.For<ILogger<ModuleMetadataCache>>();
    private readonly IModuleArtifactRepository _moduleRepository = Substitute.For<IModuleArtifactRepository>();
    private readonly IModuleHost _moduleManager = Substitute.For<IModuleHost>();
    private readonly IModuleOptionsStore _optionsStore = Substitute.For<IModuleOptionsStore>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly IOptions<ArtifactRepositoryOptions> _options = Substitute.For<IOptions<ArtifactRepositoryOptions>>();
    private readonly ArtifactRepositoryOptions _apiOptions = new() { PackageCacheLifetimeMs = 800 };

    private ServiceProvider SetupServiceProvider()
    {
        _options.Value.Returns(_apiOptions);

        _optionsStore.LoadJsonDictionary(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Dictionary<string, string?>()));

        return new ServiceCollection()
            .AddSingleton(_logger)
            .AddSingleton(_moduleManager)
            .AddSingleton(_moduleRepository)
            .AddSingleton(_options)
            .AddSingleton(_optionsStore)
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton<ModuleMetadataCache>()
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

        moduleRepository.QueryModuleMetadataArtifacts(Arg.Any<Version>(), Arg.Any<CancellationToken>()).Returns([.. moduleArtifacts]);

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
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            _moduleRepository.QueryModuleMetadataArtifacts(null, Arg.Any<CancellationToken>()).Returns([]);

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
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
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
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            _moduleRepository.QueryModuleMetadataArtifacts(null, Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleRepository.Received(1).QueryModuleMetadataArtifacts(null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_if_cache_lifetime_exceeded()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);
            await Task.Delay((int)_apiOptions.PackageCacheLifetimeMs + 200, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleRepository.Received(2).QueryModuleMetadataArtifacts(null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_when_forced()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(null, true, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleRepository.Received(2).QueryModuleMetadataArtifacts(null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_load_items_when_pre_release_flag_changed()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository);
            var version = new Version("1.0.0");

            // Act
            var metadata1 = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);
            var metadata2 = await cache.GetAvailableModuleMetadata(version, false, TestContext.Current.CancellationToken);

            // Assert
            metadata1.Should().BeEquivalentTo(metadata2);
            await _moduleRepository.Received(1).QueryModuleMetadataArtifacts(null, Arg.Any<CancellationToken>());
            await _moduleRepository.Received(1).QueryModuleMetadataArtifacts(version, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_list_of_packages_having_metadata()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
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

            _moduleRepository.QueryModuleMetadataArtifacts(null, Arg.Any<CancellationToken>())
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
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            await SetupTestModuleMetadataAssets(_moduleRepository, false);

            // Act
            var packages = await cache.GetAvailableModuleMetadata(null, false, TestContext.Current.CancellationToken);

            // Assert
            packages.Should().HaveCount(10, "8xClusterManagement + 2xDataCollectionWizard");
        }
    }

    public class GetInstalledModuleMetadata : ModuleMetadataCacheTests
    {
        [Fact]
        public async Task Should_return_infos_for_debug_modules()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            var coreContext = TestFactory.CreateBackendModuleContext(typeof(GetModuleMetadataBundlesConsumer));
            var debugContext = TestFactory.CreateBackendModuleContext(typeof(TestBackendModule), true);

            var suiteContext = new SuiteDependencyContext(coreContext, null, [debugContext]);
            SetupMetadataFiles(suiteContext);

            _moduleManager.GetContext().Returns(suiteContext);

            // Act        
            var result = await cache.GetInstalledModuleMetadata(TestContext.Current.CancellationToken);

            // Assert       
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_provide_installed_modules()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            var suiteContext = TestFactory.CreateSuiteContext();

            SetupMetadataFiles(suiteContext);

            var bundle = ModuleMetadataBundleFactory.CreateFallbackBundle(_fileSystem, suiteContext.Modules.First());

            _moduleManager.GetManifestModules().Returns([bundle]);

            // Act        
            var result = await cache.GetInstalledModuleMetadata(TestContext.Current.CancellationToken);

            // Assert       
            result.Should().HaveCount(1, "ClusterManagement");
        }

        [Fact]
        public async Task Should_create_fallback_if_metadata_is_not_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataCache>();
            var suiteContext = TestFactory.CreateSuiteContext();
            var bundle = ModuleMetadataBundleFactory.CreateFallbackBundle(_fileSystem, suiteContext.Modules.First());

            _moduleManager.GetManifestModules().Returns([bundle]);

            // Act        
            var result = await cache.GetInstalledModuleMetadata(TestContext.Current.CancellationToken);

            // Assert       
            result.Should().ContainSingle(k => k.Metadata.Description == "Generated metadata");
        }
    }
}
