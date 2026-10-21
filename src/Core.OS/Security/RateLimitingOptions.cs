using System.ComponentModel.DataAnnotations;

namespace Core.OS.Security;

/// <summary>
/// Limits on the requests the suite answers, so excess traffic gets a <c>429</c> before it saturates the device
/// (suite#2973). Per-client limits apply to each client address; static files, circuits, health checks, CSP
/// reports and loopback clients are never limited.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string ConfigSection = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Requests a client may send per second once its <see cref="Burst"/> is used up. A page load counts one to three
    /// requests, because static files and the circuit are not counted.
    /// </summary>
    [Range(1, 10_000)]
    public int PermitsPerSecond { get; set; } = 10;

    /// <summary>
    /// Requests a client may send at once after a pause, for example when several tabs reload after a restart.
    /// </summary>
    [Range(1, 100_000)]
    public int Burst { get; set; } = 50;

    /// <summary>
    /// Sign-in and account form posts a client may send per minute, on top of <see cref="PermitsPerSecond"/>.
    /// </summary>
    [Range(1, 10_000)]
    public int SignInPermitsPerMinute { get; set; } = 20;

    /// <summary>
    /// Requests of all clients together the suite processes at once; further ones are rejected right away.
    /// </summary>
    /// <remarks>
    /// Counted in flight rather than per second, so the cap follows the speed of the device. In
    /// issue-defect-management#544 an AE1400 was last healthy at about 43 requests in flight (100 virtual users,
    /// 0.95 s on average) and slowed down beyond it, so the default stops there. Requests still waiting in nginx or
    /// Kestrel are not counted, so the suite sees fewer in flight than its clients do.
    /// </remarks>
    [Range(1, 100_000)]
    public int MaxConcurrentRequests { get; set; } = 45;
}
