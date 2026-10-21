namespace Core.Shared.Security;

/// <summary>
/// Names of the rate limiting policies the suite registers, shared because the endpoints that opt into
/// them live in the UI host.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// The stricter per-client limit on the anonymous sign-in, password reset and account confirmation
    /// form posts. It counts only POST requests, so the pages themselves load under the general limit.
    /// </summary>
    public const string SignIn = "sign-in";
}
