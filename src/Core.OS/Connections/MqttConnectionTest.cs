using System.IO.Abstractions;
using Core.Shared.Extensions;
using MQTTnet;
using Sdk.Connections.Contracts;

namespace Core.OS.Connections;

public sealed class MqttConnectionTest : IConnectionTest
{
    public async Task<ConnectionTestResult> Test(IConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not MqttConnection mqttConnection)
            return ConnectionTestResultFactory.CreateFailureResult("Invalid connection type for MQTT test");

        try
        {
            using var mqttClient = new MqttClientFactory().CreateMqttClient();
            var builder = new MqttClientOptionsBuilder()
                    .WithSuiteConnection(mqttConnection, new FileSystem());

            await mqttClient.ConnectAsync(builder.Build(), cancellationToken);

            if (mqttClient.IsConnected)
            {
                await mqttClient.DisconnectAsync(new()
                {
                    Reason = MqttClientDisconnectOptionsReason.NormalDisconnection,
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return ConnectionTestResultFactory.CreateFailureResult($"MQTT connection test failed: {ex.Message}");
        }

        return new(true, null);
    }
}
