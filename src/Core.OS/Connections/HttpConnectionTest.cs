using Sdk.Connections.Contracts;

namespace Core.OS.Connections;

public sealed class HttpConnectionTest : IConnectionTest
{
    public async Task<ConnectionTestResult> Test(IConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not HttpConnection httpConnection)
            return ConnectionTestResultFactory.CreateFailureResult("Invalid connection type for HTTP test");

        HttpResponseMessage? checkingResponse = null;

        try
        {
            ArgumentNullException.ThrowIfNull(httpConnection);
            ArgumentException.ThrowIfNullOrWhiteSpace(httpConnection.BaseAddress);

            var uri = new Uri(httpConnection.BaseAddress);
            using var client = new HttpClient();

            checkingResponse = await client.GetAsync(uri, cancellationToken);
        }
        catch (Exception e)
        {
            return ConnectionTestResultFactory.CreateFailureResult($"HTTP connection test failed: {e.Message}");
        }

        if (checkingResponse is not null &&
            !checkingResponse.IsSuccessStatusCode)
        {
            return new ConnectionTestResult(false, new((int)checkingResponse.StatusCode, checkingResponse.ReasonPhrase));
        }

        return new(true, null);
    }
}
