namespace Core.OS.Instance.Extensions;

internal static class IEndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps readiness (<c>/hc</c>) and liveness. Neither is rate limited: monitoring, the Docker health check and
        /// the E2E jobs poll them, and a probe refused under load would report a working suite as down.
        /// </summary>
        internal IEndpointRouteBuilder MapHealthEndpoints()
        {
            endpoints.MapHealthChecks("/hc").DisableRateLimiting();
            endpoints.MapGet("/liveness", () => StatusCodes.Status200OK).DisableRateLimiting();

            return endpoints;
        }
    }
}
