namespace Blazor.Wasm.Client.Infrastructure.Logging;

public sealed class LogHeaderProcessingHandler(ILogger<LogHeaderProcessingHandler> logger) : MessageProcessingHandler
{
    private readonly ILogger<LogHeaderProcessingHandler> _logger = logger;

    protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogDebug("Calling {RequestMethod}:{RequestUri} (correlationId:{CorrelationId})", request.Method, request.RequestUri, correlationId);
        request.Headers.Add("CorrelationId", correlationId);
        return request;
    }

    protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken) => response;
}
