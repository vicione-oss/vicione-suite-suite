using Core.Module.Options;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.OS.Modules.Contracts;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Modules.Consumers;

public class GetModuleMetadataBundlesConsumerTests
{
    private readonly IModuleMetadataProvider _metadataProvider = Substitute.For<IModuleMetadataProvider>();
    private readonly ModuleLoaderOptions _loaderOptions = new();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetModuleMetadataBundlesConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddSingleton(_metadataProvider);
            cfg.AddSingleton(Options.Create(_loaderOptions));
            cfg.AddConsumer<GetModuleMetadataBundlesConsumer>();
        };
    }

    public sealed class GetInstalledModuleMetadata : GetModuleMetadataBundlesConsumerTests
    {
        [Fact]
        public async Task Should_return_empty_list_if_no_metadata_is_available()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(false, true);

            _metadataProvider.GetModuleMetadata(Arg.Any<GetModuleMetadataOptions>(), Arg.Any<CancellationToken>()).Returns([]);

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

            _metadataProvider.GetModuleMetadata(Arg.Any<GetModuleMetadataOptions>(), Arg.Any<CancellationToken>()).Returns(bundles);

            // Act        
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert       
            response.Bundles
                .Where(k => k.Installed)
                .Select(k => k.Metadata)
                .Should()
                .BeEquivalentTo(metadata);
        }
    }
    public sealed class AllowInstallation : GetModuleMetadataBundlesConsumerTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Should_return_the_configured_flag(bool allowInstallation)
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(false, true);

            _loaderOptions.AllowInstallation = allowInstallation;
            _metadataProvider.GetModuleMetadata(Arg.Any<GetModuleMetadataOptions>(), Arg.Any<CancellationToken>()).Returns([]);

            // Act
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.AllowInstallation.Should().Be(allowInstallation);
        }

        [Fact]
        public async Task Should_return_the_configured_flag_with_a_request_error()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(false, true);

            _loaderOptions.AllowInstallation = true;
            _metadataProvider.GetModuleMetadata(Arg.Any<GetModuleMetadataOptions>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Repository unreachable"));

            // Act
            var response = await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            response.RequestError.Should().NotBeNull();
            response.AllowInstallation.Should().BeTrue();
        }
    }

    public sealed class RequestFlags : GetModuleMetadataBundlesConsumerTests
    {
        [Fact]
        public async Task Should_pass_the_request_flags_to_the_provider()
        {
            // Arrange
            await using var tester = new MassTransitTester(_configureServices);
            var request = new GetModuleMetadataBundlesRequest(true, false, ForceRefresh: true, ResolveUpdates: true);

            _metadataProvider.GetModuleMetadata(Arg.Any<GetModuleMetadataOptions>(), Arg.Any<CancellationToken>()).Returns([]);

            // Act
            await tester.TestRequest<GetModuleMetadataBundlesResponse, GetModuleMetadataBundlesRequest>(request);

            // Assert
            await _metadataProvider.Received(1).GetModuleMetadata(new GetModuleMetadataOptions(true, false, true, true), Arg.Any<CancellationToken>());
        }
    }
}
