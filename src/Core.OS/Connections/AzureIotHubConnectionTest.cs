using Sdk.Connections.Contracts;

namespace Core.OS.Connections;

public sealed class AzureIotHubConnectionTest : IConnectionTest
{
    public Task<ConnectionTestResult> Test(IConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not AzureIotHubConnection)
            return Task.FromResult(ConnectionTestResultFactory.CreateFailureResult("Invalid connection type. Expected AzureIotHubConnection."));

        return Task.FromResult(ConnectionTestResultFactory.CreateFailureResult("Azure IoT Hub connection test is not implemented yet."));
    }
}
