using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement.Requests;
using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Communication.Capabilities;
using HostManagement.Shared.Communication.Enums;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.HostManagement.Consumers;

public class GetSystemControlCapabilitiesConsumerTests
{
    [Theory]
    [InlineData(CapabilityStatus.Enabled, CapabilityStatus.Disabled, CapabilityStatus.Enabled)]
    [InlineData(CapabilityStatus.Disabled, CapabilityStatus.Enabled, CapabilityStatus.Enabled)]
    [InlineData(CapabilityStatus.Enabled, CapabilityStatus.Enabled, CapabilityStatus.Disabled)]
    public async Task Should_return_the_system_control_capabilities(CapabilityStatus restartSystem, CapabilityStatus restartService,
        CapabilityStatus shutdownSystem)
    {
        // Arrange
        var capabilities = new SupportedCapabilities();
        capabilities.Topics.RestartSystem = restartSystem;
        capabilities.Topics.RestartService = restartService;
        capabilities.Topics.ShutdownSystem = shutdownSystem;
        var pipeClient = Substitute.For<IPipeClient>();
        pipeClient.SetupGetSupportedCapabilitiesResult(OperationStatus.Success, capabilities);
        await using var tester = CreateTester(pipeClient);

        // Act
        var response = await tester.TestRequest<GetSystemControlCapabilitiesResponse, GetSystemControlCapabilities>(new GetSystemControlCapabilities());

        // Assert
        response.Capabilities.Should().Be(new SystemControlCapabilities(restartSystem, restartService, shutdownSystem));
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Should_return_unknown_capabilities_when_host_management_does_not_report_them()
    {
        // Arrange
        var pipeClient = Substitute.For<IPipeClient>();
        pipeClient.SetupGetSupportedCapabilitiesResult(OperationStatus.Error, new SupportedCapabilities(), "Supported capabilities were not loaded.");
        await using var tester = CreateTester(pipeClient);

        // Act
        var response = await tester.TestRequest<GetSystemControlCapabilitiesResponse, GetSystemControlCapabilities>(new GetSystemControlCapabilities());

        // Assert
        response.Capabilities.Should().BeNull();
    }

    private static MassTransitTester CreateTester(IPipeClient pipeClient)
        => new(cfg =>
        {
            cfg.AddConsumer<GetSystemControlCapabilitiesConsumer>();
            cfg.AddSingleton(pipeClient);
        });
}
