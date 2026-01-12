using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using Core.OS.Connections.Mqtt;
using Core.OS.Extensions;
using Core.OS.Modules;
using Core.OS.Persistence;
using Core.Tests.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Modules;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Persistence;

public class InstanceConfigurationRepositoryTests
{
    private static readonly Guid _instanceConfigGuid = Guid.Parse("9f58350b-67a4-4f8d-8f0e-ac5dc8990e91");

    [Fact]
    public async Task Configuration_repository_should_be_serializable()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var config = new TestConfig().BuildConfiguration();
        var repository = CreateRepository();

        // Act
        await repository.StoreConfiguration(instanceId, [.. config.AsEnumerable()]);
        var instanceConfig = await repository.GetConfiguration(instanceId);

        // Assert
        Assert.NotNull(instanceConfig);

        var mqttClientOptionsOrig = config.GetMqttClientOptions();
        var mqttClientOptionsCopy = instanceConfig.GetMqttClientOptions();
        Assert.Equivalent(mqttClientOptionsOrig, mqttClientOptionsCopy, true);

        var instanceSettingsOrig = config.GetInstanceOptions();
        var instanceSettingsCopy = instanceConfig.GetInstanceOptions();

        Assert.Equivalent(instanceSettingsOrig, instanceSettingsCopy, true);
    }

    [Fact]
    public async Task Configuration_repository_should_provide_mqtt_options()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var instanceConfig = await repository.GetConfiguration(_instanceConfigGuid);

        // Assert
        Assert.NotNull(instanceConfig);

        var mqttOptions = instanceConfig.GetMqttClientOptions();
        Assert.NotNull(mqttOptions);
        Assert.NotNull(mqttOptions.ServiceClient);
        Assert.NotNull(mqttOptions.ServiceClient.Endpoint);
        Assert.NotNull(mqttOptions.WebSocketClient);
        Assert.NotNull(mqttOptions.WebSocketClient.Endpoint);
    }

    [Fact]
    public void App_settings_should_not_provide_mqtt_clients()
    {
        // Arrange
        var settingsFilePath = PathHelpers.GetAppSettingsFilePath();
        var instanceConfig = new ConfigurationBuilder()
            .AddJsonFile(settingsFilePath)
            .Build();

        // Assert
        Assert.NotNull(instanceConfig);

        var mqttOptions = instanceConfig.GetMqttClientOptions();
        Assert.NotNull(mqttOptions);
        Assert.Null(mqttOptions.ServiceClient);
        Assert.Null(mqttOptions.WebSocketClient);
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
