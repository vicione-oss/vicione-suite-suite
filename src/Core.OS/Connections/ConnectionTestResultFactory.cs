using Sdk.Connections.Contracts;

namespace Core.OS.Connections;

public static class ConnectionTestResultFactory
{
    public static ConnectionTestResult CreateFailureResult(string message, int errorCode = 500)
        => new(false, new(errorCode, message));
}
