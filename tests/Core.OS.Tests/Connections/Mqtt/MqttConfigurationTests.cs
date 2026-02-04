using Core.OS.Connections.Mqtt;
using Core.OS.Tests.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Connections.Mqtt;

public class MqttConfigurationTests
{
    [Fact]
    public void Add_mqtt_services_should_register_client_options()
    {
        // Arrange
        var config = new TestConfig()
            .AddMqttWebsocketClientOptions()
            .AddMqttServiceClientOptions();

        var services = new ServiceCollection()
            .AddLogging()
            .AddConfiguration(config)
            .AddMqttServices();

        // Act
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var clientOptions = serviceProvider.GetRequiredService<IOptions<MqttClientOptions>>().Value;
        Assert.NotNull(clientOptions.WebSocketClient);
        Assert.Equal("wss://localhost/mqtt", clientOptions.WebSocketClient.Endpoint);
        Assert.Equal(5001, clientOptions.WebSocketClient.Port);
        Assert.Equal("user-wsc", clientOptions.WebSocketClient.UserName);
        Assert.Equal("pwd-wsc", clientOptions.WebSocketClient.Password);
        Assert.Equal("topic-wsc", clientOptions.WebSocketClient.TopicFilter);

        Assert.NotNull(clientOptions.ServiceClient);
        Assert.Equal("localhost", clientOptions.ServiceClient.Endpoint);
        Assert.Equal(1883, clientOptions.ServiceClient.Port);
        Assert.Equal("user-sc", clientOptions.ServiceClient.UserName);
        Assert.Equal("pwd-sc", clientOptions.ServiceClient.Password);
        Assert.Equal("topic-sc", clientOptions.ServiceClient.TopicFilter);
    }
}
