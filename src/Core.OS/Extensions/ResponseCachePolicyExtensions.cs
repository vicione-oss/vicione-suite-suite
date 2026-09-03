namespace Core.OS.Extensions;

internal static class ResponseCachePolicyExtensions
{
    private const string NoCachePolicy = "no-store, no-cache, must-revalidate";

    /// <summary>
    /// Forbids caching of every dynamic response, so no page or payload holding user data is
    /// kept by the browser or an intermediate proxy.
    /// </summary>
    /// <remarks>
    /// Register after the static-file middleware and before routing. Static assets carry no user
    /// data and stay cacheable because the static-file middleware answers them before this runs.
    /// The headers are written from <see cref="HttpResponse.OnStarting(Func{object,Task},object)"/>
    /// rather than up front: Blazor sets its own cache header while rendering, and only a callback
    /// registered this early runs late enough to take precedence.
    /// </remarks>
    public static IApplicationBuilder UseResponseCachePolicy(this IApplicationBuilder app)
        => app.Use((context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var headers = ((HttpContext)state).Response.Headers;
                headers.CacheControl = NoCachePolicy;
                headers.Pragma = "no-cache";
                headers.Expires = "0";
                return Task.CompletedTask;
            }, context);

            return next();
        });
}
