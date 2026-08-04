using Core.OS.Connections.Mqtt;
using Core.OS.Tests.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Connections.Mqtt;

public sealed class MqttConfigurationTests
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
        clientOptions.WebSocketClient.Endpoint.Should().Be("wss://localhost/mqtt");
        clientOptions.WebSocketClient.Port.Should().Be(5001);
        clientOptions.WebSocketClient.UserName.Should().Be("user-wsc");
        clientOptions.WebSocketClient.Password.Should().Be("pwd-wsc");
        clientOptions.WebSocketClient.TopicFilter.Should().Be("topic-wsc");

        Assert.NotNull(clientOptions.ServiceClient);
        clientOptions.ServiceClient.Endpoint.Should().Be("localhost");
        clientOptions.ServiceClient.Port.Should().Be(1883);
        clientOptions.ServiceClient.UserName.Should().Be("user-sc");
        clientOptions.ServiceClient.Password.Should().Be("pwd-sc");
        clientOptions.ServiceClient.TopicFilter.Should().Be("topic-sc");
    }
}
