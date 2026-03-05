using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using AwesomeAssertions;
using Core.Module;
using Core.Module.Options;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.OS.Modules.Contracts;
using Core.OS.Modules.Factories;
using Core.OS.Modules.Services;
using Core.OS.Tests.Modules.Consumers;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Modules;
using TestModule.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Services;

public class ModuleMetadataProviderTests
{
    private readonly ILogger<ModuleMetadataProvider> _logger = Substitute.For<ILogger<ModuleMetadataProvider>>();
    private readonly IModuleArtifactCache _artifactCache = Substitute.For<IModuleArtifactCache>();
    private readonly IModuleHost _moduleManager = Substitute.For<IModuleHost>();
    private readonly IModuleOptionsStore _optionsStore = Substitute.For<IModuleOptionsStore>();
    private readonly IModulePackageOperationStore _operationStore = Substitute.For<IModulePackageOperationStore>();

    private readonly IOptions<ModuleLoaderOptions> _loaderOptions = Substitute.For<IOptions<ModuleLoaderOptions>>();
    private readonly MockFileSystem _fileSystem = new();

    private ServiceProvider SetupServiceProvider()
    {
        _optionsStore.LoadJsonDictionary(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Dictionary<string, string?>()));

        _loaderOptions.Value.Returns(new ModuleLoaderOptions());

        return new ServiceCollection()
            .AddSingleton(_artifactCache)
            .AddSingleton(_loaderOptions)
            .AddSingleton(_logger)
            .AddSingleton(_moduleManager)
            .AddSingleton(_optionsStore)
            .AddSingleton(_operationStore)
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton<ModuleMetadataProvider>()
            .BuildServiceProvider();
    }

    private void SetupMetadataFiles(SuiteDependencyContext suiteContext)
    {
        var assembly = Assembly.GetAssembly(typeof(GetModuleMetadataBundlesConsumerTests));
        var metadataResource = $"{TestFactory.ModuleResourceNamespace}.{TestFactory.ClusterManagementMetadataResource}";

        foreach (var module in suiteContext.Modules)
        {
            var metadataPath = _fileSystem.Path.Combine(module.AssemblyFolder, ModuleConstants.MetadataFileName);

            _fileSystem.AddDirectory(module.AssemblyFolder);
            _fileSystem.AddFileFromEmbeddedResource(metadataPath, assembly, metadataResource);
        }
    }

    public class GetModuleMetadata : ModuleMetadataProviderTests
    {
        [Fact]
        public async Task Should_return_empty_list_if_no_modules_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var getOptions = new GetModuleMetadataOptions(false, true);
            _artifactCache.GetAvailableModuleMetadata(null, false, Arg.Any<CancellationToken>()).Returns([]);

            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();

            // Act
            var metadata = await cache.GetModuleMetadata(getOptions, TestContext.Current.CancellationToken);

            // Assert
            metadata.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_call_metadata_cache()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var version = new Version(1, 1, 1);
            var getOptions = new GetModuleMetadataOptions(false, true, true);

            // Act
            var metadata = await cache.GetModuleMetadata(getOptions, TestContext.Current.CancellationToken);

            // Assert
            await _artifactCache.Received(1).GetAvailableModuleMetadata(Arg.Any<Version>(), getOptions.ForceRefresh, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_return_infos_for_debug_modules()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var coreContext = TestFactory.CreateBackendModuleContext(typeof(GetModuleMetadataBundlesConsumer));
            var debugContext = TestFactory.CreateBackendModuleContext(typeof(TestBackendModule), true);
            var suiteContext = new SuiteDependencyContext(coreContext, null, [debugContext]);
            SetupMetadataFiles(suiteContext);

            _moduleManager.GetContext().Returns(suiteContext);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, false), TestContext.Current.CancellationToken);

            // Assert       
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_provide_installed_modules()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var suiteContext = TestFactory.CreateSuiteContext();
            var getOptions = new GetModuleMetadataOptions(true, false);

            SetupMetadataFiles(suiteContext);

            var bundle = ModuleMetadataBundleFactory.CreateFallbackBundle(_fileSystem, suiteContext.Modules.First());

            _moduleManager.GetManifestModules().Returns([bundle]);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, false), TestContext.Current.CancellationToken);

            // Assert       
            result.Should().HaveCount(1, "ClusterManagement");
        }

        [Fact]
        public async Task Should_create_fallback_if_metadata_is_not_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var suiteContext = TestFactory.CreateSuiteContext();
            var bundle = ModuleMetadataBundleFactory.CreateFallbackBundle(_fileSystem, suiteContext.Modules.First());

            _moduleManager.GetManifestModules().Returns([bundle]);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, false), TestContext.Current.CancellationToken);

            // Assert       
            result.Should().ContainSingle(k => k.Metadata.Description == "Generated metadata");
        }

        [Fact]
        public async Task Should_apply_pending_operations()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var suiteContext = TestFactory.CreateSuiteContext();
            var bundle = ModuleMetadataBundleFactory.CreateFallbackBundle(_fileSystem, suiteContext.Modules.First());

            var package = new ModuleDependencyPackage
            {
                Name = bundle.Metadata.Name,
                Version = bundle.Metadata.Version
            };
            var pendingOperation = new ModulePackageOperation(package, ModulePackageOperationKind.Uninstall);

            _operationStore.GetEnqueuedOperations(Arg.Any<CancellationToken>()).Returns([pendingOperation]);
            _moduleManager.GetManifestModules().Returns([bundle]);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, false), TestContext.Current.CancellationToken);

            // Assert       
            result.Should().ContainSingle(k => k.PendingOperation == pendingOperation);
        }

        [Fact]
        public async Task Should_apply_environment_options()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var suiteContext = TestFactory.CreateSuiteContext();
            var bundle = ModuleMetadataBundleFactory.CreateFallbackBundle(_fileSystem, suiteContext.Modules.First());

            _loaderOptions.Value.Returns(new ModuleLoaderOptions() { AllowInstallation = true });
            _moduleManager.GetManifestModules().Returns([bundle]);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, false), TestContext.Current.CancellationToken);

            // Assert       
            result.Should().ContainSingle(k => k.CanBeModified);
        }

        [Fact]
        public async Task Should_return_installed_modules_with_available_versions()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var metadata = await TestFactory.GetEmbeddedModuleMetadata();
            var installed = metadata
                .Take(2)
                .Select(k => new ModuleMetadataBundle
                {
                    ModuleId = k.Name,
                    Metadata = k,
                    Installed = true
                })
                .ToList();

            var module = installed.First();
            var allowedVersion = "0.35.0";
            var available = new List<ModuleMetadata>
            {
                new()
                {
                    Name = module.ModuleId,
                    Version = $"{module.Metadata.Version}-ci23423423",
                    MinSuiteSdkVersion = module.Metadata.MinSuiteSdkVersion,
                },
                new()
                {
                    Name = module.ModuleId,
                    Version = allowedVersion,
                    MinSuiteSdkVersion = module.Metadata.MinSuiteSdkVersion,
                },
            };

            _moduleManager.GetManifestModules().Returns(installed);
            _artifactCache.GetAvailableModuleMetadata(Arg.Any<Version>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(available);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, true), TestContext.Current.CancellationToken);

            // Assert       
            result
                .First(k => k.ModuleId == metadata.First().Name)
                .AvailableVersions
                .Should()
                .ContainSingle(k => k == allowedVersion);
        }

        [Fact]
        public async Task Should_return_installed_modules_with_available_pre_release_versions()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var metadata = await TestFactory.GetEmbeddedModuleMetadata();
            var installed = metadata
                .Take(2)
                .Select(k => new ModuleMetadataBundle
                {
                    ModuleId = k.Name,
                    Metadata = k,
                    Installed = true
                })
                .ToList();

            var module = installed.First();
            var moduleVersion = Version.Parse(module.Metadata.Version);
            var preVersion = new Version(moduleVersion.Major, moduleVersion.Minor, moduleVersion.Build + 1);

            var available = new List<ModuleMetadata>
            {
                new()
                {
                    Name = module.ModuleId,
                    Version = $"{preVersion}-ci23423423",
                    MinSuiteSdkVersion = module.Metadata.MinSuiteSdkVersion,
                },
                new()
                {
                    Name = module.ModuleId,
                    Version = $"{preVersion}-ci1245423",
                    MinSuiteSdkVersion = module.Metadata.MinSuiteSdkVersion,
                },
            };

            _moduleManager.GetManifestModules().Returns(installed);
            _artifactCache.GetAvailableModuleMetadata(Arg.Any<Version>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(available);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, true), TestContext.Current.CancellationToken);

            // Assert       
            result
                .First(k => k.ModuleId == metadata.First().Name)
                .AvailableVersions
                .Should()
                .HaveCount(2, "the 2 pre release versions should be available");
        }

        [Fact]
        public async Task Should_only_return_installed_modules_with_versions_if_allow_install_is_false()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var metadata = await TestFactory.GetEmbeddedModuleMetadata();
            var installed = metadata
                .Take(1)
                .Select(k => new ModuleMetadataBundle
                {
                    ModuleId = k.Name,
                    Metadata = k,
                    Installed = true,
                })
                .ToList();

            var module = installed.First();
            var allowedVersion = "0.35.0";
            var available = new List<ModuleMetadata>
            {
                new()
                {
                    Name = module.ModuleId,
                    Version = $"{module.Metadata.Version}-ci23423423",
                    MinSuiteSdkVersion = module.Metadata.MinSuiteSdkVersion,
                },
                new()
                {
                    Name = module.ModuleId,
                    Version = allowedVersion,
                    MinSuiteSdkVersion = module.Metadata.MinSuiteSdkVersion,
                },
            };

            _artifactCache.GetAvailableModuleMetadata(Arg.Any<Version>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(available);
            _moduleManager.GetManifestModules().Returns(installed);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, true), TestContext.Current.CancellationToken);

            // Assert
            result.Should().HaveCount(1, "only the first one is installed");
            result
                .First(k => k.ModuleId == metadata.First().Name)
                .AvailableVersions
                .Should()
                .ContainSingle(k => k == allowedVersion);
        }

        [Fact]
        public async Task Should_throw_on_failure()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var request = new GetModuleMetadataBundlesRequest(true, false);

            _moduleManager.GetManifestModules().Throws<InvalidOperationException>();

            // Act        
            var action = () => cache.GetModuleMetadata(new GetModuleMetadataOptions(true, true), TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_return_empty_list_if_no_modules_are_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();

            _artifactCache.GetAvailableModuleMetadata(Arg.Any<Version>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, false), TestContext.Current.CancellationToken);

            // Assert
            result.Where(k => !k.Installed).Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_available_metadata_assets()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var metadata = await TestFactory.GetEmbeddedModuleMetadata(false);

            _artifactCache.GetAvailableModuleMetadata(Arg.Any<Version>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(metadata);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(false, true), TestContext.Current.CancellationToken);

            // Assert
            result
                .Where(k => !k.Installed)
                .Select(k => k.Metadata)
                .Should()
                .BeEquivalentTo(metadata);
        }

        [Fact]
        public async Task Should_return_other_metadata_assets_if_no_modules_are_installed()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var metadata = await TestFactory.GetEmbeddedModuleMetadata(false);

            _moduleManager.GetManifestModules().Returns([]);
            _artifactCache.GetAvailableModuleMetadata(Arg.Any<Version>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(metadata);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, true), TestContext.Current.CancellationToken);

            // Assert
            result.Should().HaveCount(metadata.Count, "all available metadata assets should be returned if no modules are installed");
        }

        [Fact]
        public async Task Should_return_available_metadata_assets_only_for_installed_modules_if_install_flag_is_disabled()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider();
            var cache = serviceProvider.GetRequiredService<ModuleMetadataProvider>();
            var request = new GetModuleMetadataBundlesRequest(true, true);
            var metadata = await TestFactory.GetEmbeddedModuleMetadata(true);
            var installed = metadata
                .Select(k => new ModuleMetadataBundle
                {
                    ModuleId = k.Name,
                    Metadata = k,
                    Installed = true,
                    CanBeModified = false
                })
                .First();

            _moduleManager.GetManifestModules().Returns([installed]);
            _artifactCache.GetAvailableModuleMetadata(Arg.Any<Version>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(metadata);

            // Act        
            var result = await cache.GetModuleMetadata(new GetModuleMetadataOptions(true, true), TestContext.Current.CancellationToken);

            // Assert
            result
                .All(k => k.Metadata.Name == installed.Metadata.Name)
                .Should()
                .BeTrue("Only available metadata assets of installed module");
        }
    }
}
