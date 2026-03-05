using AwesomeAssertions;
using Core.OS.Modules;
using Core.OS.Modules.Consumers;
using Core.OS.Modules.Contracts;
using Core.Shared.Modules.Contracts;
using Core.Shared.Modules.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Modules.Consumers;

public class GetModuleMetadataBundlesConsumerTests
{
    private readonly IModuleMetadataProvider _metadataProvider = Substitute.For<IModuleMetadataProvider>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetModuleMetadataBundlesConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddSingleton(_metadataProvider);
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
}
