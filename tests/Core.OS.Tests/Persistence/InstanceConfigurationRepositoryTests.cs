using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Core.OS.Connections.Mqtt;
using Core.OS.Instance.Extensions;
using Core.OS.Modules;
using Core.OS.Persistence;
using Core.Tests.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Persistence;

public class InstanceConfigurationRepositoryTests
{
    private static readonly Guid _instanceConfigGuid = Guid.Parse("9f58350b-67a4-4f8d-8f0e-ac5dc8990e91");

    [Fact]
    public async Task Should_be_serializable()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var config = new TestConfig().BuildConfiguration();
        var repository = CreateRepository();

        // Act
        await repository.StoreConfiguration(instanceId, [.. config.AsEnumerable()]);
        var instanceConfig = await repository.GetConfiguration(instanceId);

        // Assert
        instanceConfig.Should().NotBeNull();

        var mqttClientOptionsOrig = config.GetMqttClientOptions();
        var mqttClientOptionsCopy = instanceConfig.GetMqttClientOptions();
        mqttClientOptionsCopy.Should().BeEquivalentTo(mqttClientOptionsOrig);

        var instanceSettingsOrig = config.GetInstanceOptions();
        var instanceSettingsCopy = instanceConfig.GetInstanceOptions();

        instanceSettingsCopy.Should().BeEquivalentTo(instanceSettingsOrig);
    }

    [Fact]
    public async Task Should_provide_mqtt_options()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var instanceConfig = await repository.GetConfiguration(_instanceConfigGuid);

        // Assert
        instanceConfig.Should().NotBeNull();

        var mqttOptions = instanceConfig.GetMqttClientOptions();
        mqttOptions.Should().NotBeNull();
        mqttOptions.ServiceClient.Should().NotBeNull();
        mqttOptions.ServiceClient!.Endpoint.Should().NotBeNull();
        mqttOptions.WebSocketClient.Should().NotBeNull();
        mqttOptions.WebSocketClient!.Endpoint.Should().NotBeNull();
    }

    [Fact]
    public void Should_not_provide_mqtt_clients_from_app_settings()
    {
        // Arrange
        var settingsFilePath = PathHelpers.GetAppSettingsFilePath();

        // Act
        var instanceConfig = new ConfigurationBuilder()
            .AddJsonFile(settingsFilePath)
            .Build();

        // Assert
        instanceConfig.Should().NotBeNull();

        var mqttOptions = instanceConfig.GetMqttClientOptions();
        mqttOptions.Should().NotBeNull();
        mqttOptions.ServiceClient.Should().BeNull();
        mqttOptions.WebSocketClient.Should().BeNull();
    }

    private static InstanceConfigurationRepository CreateRepository()
    {
        var wsMock = Substitute.For<IWorkspaceProvider<SystemBackendModule>>();
        wsMock.Home.Returns(Directory.GetCurrentDirectory());

        var fsMock = new MockFileSystem();

        var configPath = Path.Combine(wsMock.Home, "instances", $"{_instanceConfigGuid}.json");
        fsMock.AddFileFromEmbeddedResource(configPath, Assembly.GetExecutingAssembly(), $@"Core.OS.Tests.Resources.{_instanceConfigGuid}.json");

        var loggerMock = Substitute.For<ILogger<InstanceConfigurationRepository>>();

        return new InstanceConfigurationRepository(wsMock, fsMock, loggerMock);
    }
}
