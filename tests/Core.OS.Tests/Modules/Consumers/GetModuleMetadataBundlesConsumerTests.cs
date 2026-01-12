using Core.Module.Options;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Requests;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Modules;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Consumers;

public class GetModuleMetadataBundlesConsumerTests
{
    private readonly IModuleMetadataCache _metadataCache = Substitute.For<IModuleMetadataCache>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly ModuleLoaderOptions _loaderOptions = new();

    public GetModuleMetadataBundlesConsumerTests()
    {
        var options = Substitute.For<IOptions<ModuleLoaderOptions>>();
        options.Value.Returns(_loaderOptions);

        _configureServices = cfg =>
        {
            cfg.AddSingleton(_metadataCache);
            cfg.AddSingleton(options);
            cfg.AddConsumer<GetModuleMetadataBundlesConsumer>();
        };
    }

    public class GetInstalledModuleMetadata : GetModuleMetadataBundlesConsumerTests
    {
        [Fact]
        public async Task Should_return_empty_list_if_no_metadata_is_available()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(false, true);

            _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.Bundles.Where(k => !k.Installed).Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_infos_of_installed_modules()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, false);
            var metadata = await TestFactory.GetEmbeddedModuleMetadata();
            var bundles = metadata
                .Select(k => new ModuleMetadataBundle
                {
                    ModuleId = k.Name,
                    Metadata = k,
                    Installed = true
                })
                .ToList();

            _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>()).Returns(bundles);

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert       
            response.Bundles
                .Where(k => k.Installed)
                .Select(k => k.Metadata)
                .Should()
                .BeEquivalentTo(metadata);
        }


        [Fact]
        public async Task Should_return_installed_modules_with_available_versions()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, true, null, true);
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

            _metadataCache.GetAvailableModuleMetadata(request.SdkVersion, request.ForceRefresh, Arg.Any<CancellationToken>()).Returns(available);
            _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>()).Returns(installed);

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert       
            response.Bundles
                .First(k => k.ModuleId == metadata.First().Name)
                .AvailableVersions
                .Should()
                .ContainSingle(k => k == allowedVersion);
        }

        [Fact]
        public async Task Should_return_installed_modules_with_available_pre_release_versions()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, true, null, true);
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

            _metadataCache.GetAvailableModuleMetadata(request.SdkVersion, request.ForceRefresh, Arg.Any<CancellationToken>()).Returns(available);
            _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>()).Returns(installed);

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert       
            response.Bundles
                .First(k => k.ModuleId == metadata.First().Name)
                .AvailableVersions
                .Should()
                .HaveCount(2, "the 2 pre release versions should be available");
        }

        [Fact]
        public async Task Should_only_return_installed_modules_with_versions_if_allow_install_is_false()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, true, null, true);
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

            _metadataCache.GetAvailableModuleMetadata(request.SdkVersion, request.ForceRefresh, Arg.Any<CancellationToken>()).Returns(available);
            _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>()).Returns(installed);

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.Bundles.Should().HaveCount(1, "only the first one is installed");
            response.Bundles
                .First(k => k.ModuleId == metadata.First().Name)
                .AvailableVersions
                .Should()
                .ContainSingle(k => k == allowedVersion);
        }

        [Fact]
        public async Task Should_return_error_info_on_failure()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, false);

            _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>()).ThrowsAsync<InvalidOperationException>();

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.RequestError.Should().NotBeNull();
        }
    }

    public class GetAvailableModuleMetadata : GetModuleMetadataBundlesConsumerTests
    {
        [Fact]
        public async Task Should_return_empty_list_if_no_modules_are_available()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, false);

            _metadataCache.GetAvailableModuleMetadata(request.SdkVersion, request.ForceRefresh, Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.Bundles.Where(k => !k.Installed).Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_available_metadata_assets()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(false, true);
            var metadata = await TestFactory.GetEmbeddedModuleMetadata(false);

            _metadataCache.GetAvailableModuleMetadata(request.SdkVersion, request.ForceRefresh, Arg.Any<CancellationToken>()).Returns(metadata);

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.Bundles
                .Where(k => !k.Installed)
                .Select(k => k.Metadata)
                .Should()
                .BeEquivalentTo(metadata);
        }

        [Fact]
        public async Task Should_not_return_metadata_assets_if_install_flag_is_disabled_and_no_modules_installed()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, true);
            var metadata = await TestFactory.GetEmbeddedModuleMetadata(false);

            _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>()).Returns([]);
            _loaderOptions.AllowInstallation = false;

            _metadataCache.GetAvailableModuleMetadata(null, false, Arg.Any<CancellationToken>()).Returns(metadata);

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.Bundles.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_return_available_metadata_assets_only_for_installed_modules_if_install_flag_is_disabled()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, true);
            var metadata = await TestFactory.GetEmbeddedModuleMetadata(true);
            var installed = metadata
                .Select(k => new ModuleMetadataBundle
                {
                    ModuleId = k.Name,
                    Metadata = k,
                    Installed = true
                })
                .First();

            _metadataCache.GetInstalledModuleMetadata(Arg.Any<CancellationToken>()).Returns([installed]);
            _loaderOptions.AllowInstallation = false;

            _metadataCache.GetAvailableModuleMetadata(null, false, Arg.Any<CancellationToken>()).Returns(metadata);

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.Bundles
                .All(k => k.Metadata.Name == installed.Metadata.Name)
                .Should()
                .BeTrue("Only available metadata assets of installed module");
        }
    }
}
