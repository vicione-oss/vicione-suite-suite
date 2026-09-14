namespace Core.Shared.Security;

/// <summary>
/// The route the Content Security Policy of ADR-006 names as its own report sink. Shared because
/// the suite maps it, the policy points at it, and the onboarding redirect has to leave it alone.
/// </summary>
public static class CspViolationReporting
{
    public const string Route = "/csp-report";
}
