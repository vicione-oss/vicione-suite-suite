using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Core.Shared.HostManagement;
using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using Microsoft.Extensions.Options;

namespace Core.OS.Tests.HostManagement;

public sealed class MockPipeClientTests
{
    private const string CapabilitiesFile = "SupportedCapabilities.json";

    private readonly MockFileSystem _fileSystem = new();

    [Fact]
    public async Task Should_enable_all_capabilities_without_a_capabilities_file()
    {
        // Arrange
        var pipeClient = CreateMockPipeClient(supportedCapabilitiesJsonFile: null);

        // Act
        var result = await pipeClient.GetSupportedCapabilities(TestContext.Current.CancellationToken);

        // Assert
        result!.Status.Should().Be(OperationStatus.Success);
        JsonSerializer.Serialize(result.SupportedCapabilities, CapabilitySourceGenerationContext.Default.SupportedCapabilities)
            .Should().NotContain(nameof(CapabilityStatus.Disabled));
    }

    [Fact]
    public async Task Should_apply_the_capabilities_file_on_top_of_all_enabled()
    {
        // Arrange
        _fileSystem.AddFile(CapabilitiesFile, new MockFileData("""
            {
              "Topics": { "RestartSystem": "Disabled" },
              "Settings": { "DNS": { "Hostname": { "Capability": "Disabled" } } }
            }
            """));
        var pipeClient = CreateMockPipeClient(CapabilitiesFile);

        // Act
        var result = await pipeClient.GetSupportedCapabilities(TestContext.Current.CancellationToken);

        // Assert
        var capabilities = result!.SupportedCapabilities;
        capabilities.Topics.RestartSystem.Should().Be(CapabilityStatus.Disabled);
        capabilities.Settings.DNS.Hostname.Capability.Should().Be(CapabilityStatus.Disabled);
        capabilities.Topics.RestartService.Should().Be(CapabilityStatus.Enabled);
        capabilities.Settings.DNS.MulticastDNS.Capability.Should().Be(CapabilityStatus.Enabled);
    }

    [Fact]
    public void Should_fail_when_the_capabilities_file_is_missing()
    {
        // Arrange + Act
        var act = () => CreateMockPipeClient(CapabilitiesFile);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    private MockPipeClient CreateMockPipeClient(string? supportedCapabilitiesJsonFile)
    {
        var instanceOptions = new InstanceOptions
        {
            HomeDirectory = _fileSystem.Path.GetFullPath("AppData"),
            CacheDirectory = _fileSystem.Path.GetFullPath("Cache"),
            BackupDirectory = _fileSystem.Path.GetFullPath("Backup"),
            Type = Sdk.Instance.InstanceType.Standalone,
        };
        var mockOptions = new MockPipeClientOptions
        {
            Enabled = true,
            DataSource = MockPipeClientDataSource.SystemConfigurationEmbedded,
            SupportedCapabilitiesJsonFile = supportedCapabilitiesJsonFile,
        };

        return new MockPipeClient(_fileSystem,
            Substitute.For<Microsoft.Extensions.Hosting.IHostApplicationLifetime>(),
            Options.Create(mockOptions),
            Options.Create(instanceOptions));
    }
}
