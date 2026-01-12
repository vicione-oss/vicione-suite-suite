using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class GetAvailableSuiteVersionsConsumerTests
{
    [Fact]
    public async Task Should_return_suite_versions()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetAvailableSuiteVersionsConsumer>();
            cfg.AddMockPipeClientSystemConfiguration();
        });
        var request = new GetAvailableSuiteVersions();

        // Act
        var response = await tester.TestRequest<GetAvailableSuiteVersionsResponse, GetAvailableSuiteVersions>(request);

        // Assert
        response.Should().NotBeNull();
        response.Versions.Should().NotBeEmpty();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_response_with_error_info_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<GetAvailableSuiteVersionsConsumer>();
            cfg.AddSingleton(Substitute.For<IPipeClient>());
        });
        var request = new GetAvailableSuiteVersions();

        // Act
        var response = await tester.TestRequest<GetAvailableSuiteVersionsResponse, GetAvailableSuiteVersions>(request);

        // Assert
        response.Should().NotBeNull();
        response.Versions.Should().BeEmpty();
        response.RequestError.Should().NotBeNull();
    }
}
