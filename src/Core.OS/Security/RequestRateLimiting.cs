using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Core.Shared.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace Core.OS.Security;

/// <summary>
/// The limiters of suite#2973: a token bucket per client, a cap on the requests of all clients in flight, and the
/// stricter <see cref="RateLimitPolicies.SignIn"/> policy for the endpoints that opt into it.
/// </summary>
internal static partial class RequestRateLimiting
{
    private const string GlobalCapPartition = "global";

    /// <summary>
    /// The circuit (negotiation, WebSocket, long polling, reconnects) and the framework's scripts. The framework maps
    /// them, so they are recognised by path; the suite's own unlimited endpoints carry <c>DisableRateLimiting</c>.
    /// </summary>
    private static readonly PathString[] UnlimitedPaths = [new("/_blazor"), new("/_framework")];

    /// <summary>
    /// The partition of every request that is not limited; one key, because a limiter that never refuses holds no state.
    /// </summary>
    private static readonly RateLimitPartition<string> Unlimited = RateLimitPartition.GetNoLimiter(string.Empty);

    /// <summary>
    /// What a request rejected by the global cap is told to wait, because a concurrency limiter reports no time of
    /// its own.
    /// </summary>
    private static readonly TimeSpan GlobalCapRetryAfter = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan RejectionLogInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Makes looking up and recording a logged rejection one step: a burst is rejected on many threads at once, and
    /// each would otherwise find the entry missing and log.
    /// </summary>
    private static readonly Lock RejectionLogGate = new();

    public static void Configure(RateLimiterOptions limiter, RateLimitingOptions options)
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.OnRejected = (rejection, _) => OnRejected(rejection, options);

        if (!options.Enabled)
        {
            KeepSignInPolicyResolvable(limiter);
            return;
        }

        limiter.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(context => PerClient(context, options)),
            PartitionedRateLimiter.Create<HttpContext, string>(context => GlobalCap(context, options)));

        limiter.AddPolicy(RateLimitPolicies.SignIn, context => SignIn(context, options));
    }

    /// <summary>
    /// The address a request is counted against, or <see langword="null"/> for a request that is never limited.
    /// </summary>
    /// <remarks>
    /// Runs after <c>UseForwardedHeaders</c>, so behind a trusted proxy this is the forwarded client. Loopback stays
    /// unlimited: every request through the packaged nginx carries its client's address, so what is left on loopback
    /// is the suite calling itself, such as modules through <c>ILocalHttpClient</c>. IPv6 clients are counted per
    /// address, not per /64, because on a device's LAN every client shares the same /64.
    /// </remarks>
    internal static string? ClientOf(HttpContext context)
    {
        if (UnlimitedPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
            return null;

        var address = context.Connection.RemoteIpAddress;
        if (address is null)
            return null;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return IPAddress.IsLoopback(address) ? null : address.ToString();
    }

    /// <summary>
    /// Registers the sign-in policy without a limit, because endpoints still name it and the middleware refuses a policy
    /// name it does not know.
    /// </summary>
    private static void KeepSignInPolicyResolvable(RateLimiterOptions limiter)
        => limiter.AddPolicy(RateLimitPolicies.SignIn, _ => Unlimited);

    private static RateLimitPartition<string> PerClient(HttpContext context, RateLimitingOptions options)
    {
        var client = ClientOf(context);
        if (client is null)
            return Unlimited;

        return RateLimitPartition.GetTokenBucketLimiter(client, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.Burst,
            TokensPerPeriod = options.PermitsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
        });
    }

    private static RateLimitPartition<string> GlobalCap(HttpContext context, RateLimitingOptions options)
    {
        if (ClientOf(context) is null)
            return Unlimited;

        return RateLimitPartition.GetConcurrencyLimiter(GlobalCapPartition, _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = options.MaxConcurrentRequests,
            QueueLimit = 0,
        });
    }

    /// <summary>
    /// A token bucket that refills one sign-in at a time, so a client that used up its minute gets the next attempt
    /// after <c>60 / SignInPermitsPerMinute</c> seconds rather than at the start of the next window.
    /// </summary>
    private static RateLimitPartition<string> SignIn(HttpContext context, RateLimitingOptions options)
    {
        var client = HttpMethods.IsPost(context.Request.Method) ? ClientOf(context) : null;
        if (client is null)
            return Unlimited;

        return RateLimitPartition.GetTokenBucketLimiter(client, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = options.SignInPermitsPerMinute,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1) / options.SignInPermitsPerMinute,
            QueueLimit = 0,
        });
    }

    /// <summary>
    /// Adds <c>Retry-After</c> and logs the first rejection of a client, or of the global cap, per minute — an attack
    /// is rejected thousands of times, and every entry is a write to the device's flash.
    /// </summary>
    /// <remarks>
    /// Only the per-client token buckets report a retry time, so a lease without one was refused by the global cap.
    /// </remarks>
    private static ValueTask OnRejected(OnRejectedContext rejection, RateLimitingOptions options)
    {
        var context = rejection.HttpContext;
        var isClientLimit = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter);
        var retryAfterSeconds = SetRetryAfter(context.Response, isClientLimit ? retryAfter : GlobalCapRetryAfter);

        var partition = isClientLimit ? ClientOf(context) ?? string.Empty : GlobalCapPartition;
        if (!IsFirstRejectionThisMinute(context.RequestServices, partition))
            return ValueTask.CompletedTask;

        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RequestRateLimiting));
        if (isClientLimit)
            LogClientLimited(logger, partition, retryAfterSeconds);
        else
            LogGlobalCapReached(logger, options.MaxConcurrentRequests);

        return ValueTask.CompletedTask;
    }

    /// <returns>The whole seconds the header names, at least one.</returns>
    private static int SetRetryAfter(HttpResponse response, TimeSpan retryAfter)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        return seconds;
    }

    /// <summary>
    /// Records a rejection of <paramref name="partition"/>, telling whether it is the first one within
    /// <see cref="RejectionLogInterval"/>.
    /// </summary>
    private static bool IsFirstRejectionThisMinute(IServiceProvider services, string partition)
    {
        var cache = services.GetRequiredService<IMemoryCache>();
        var key = (typeof(RequestRateLimiting), partition);

        lock (RejectionLogGate)
        {
            if (cache.TryGetValue(key, out _))
                return false;

            cache.Set(key, true, RejectionLogInterval);

            return true;
        }
    }

    [LoggerMessage(LogLevel.Warning,
        "Requests from {Client} exceed its rate limit and are rejected with 429 for {RetryAfterSeconds} s. "
        + "Further rejections of this client are not logged for a minute.")]
    private static partial void LogClientLimited(ILogger logger, string client, int retryAfterSeconds);

    [LoggerMessage(LogLevel.Warning,
        "Requests are rejected with 429 because {MaxConcurrentRequests} requests are in progress. "
        + "Further rejections are not logged for a minute.")]
    private static partial void LogGlobalCapReached(ILogger logger, int maxConcurrentRequests);
}
