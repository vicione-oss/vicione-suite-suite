namespace Core.Tests.Tools;

/// <summary>
/// Resolves settings for integration and E2E tests from environment variables, so CI can
/// point tests at the services and accounts it provides. Settings with a safe default
/// (hosts, ports, URLs, ...) fall back to a local default suitable for a service started
/// locally (e.g. mailpit via docker or mise); settings that must not have a default
/// (credentials, ...) are required and throw when unset. See docs/integration-testing.md.
/// </summary>
public static class IntegrationServiceSettings
{
    /// <summary>
    /// Returns the value of <paramref name="envVar"/>, or <paramref name="defaultValue"/>
    /// when the variable is unset or empty.
    /// </summary>
    public static string GetValue(string envVar, string defaultValue)
        => Environment.GetEnvironmentVariable(envVar) is { Length: > 0 } value ? value : defaultValue;

    /// <summary>
    /// Returns the value of <paramref name="envVar"/>, or throws when it is unset or empty.
    /// Use this for settings that have no safe default and must be supplied by the
    /// environment (e.g. credentials).
    /// </summary>
    public static string GetRequiredValue(string envVar)
        => Environment.GetEnvironmentVariable(envVar) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Required environment variable '{envVar}' is not set. It has no default and must "
                + "be supplied by the environment so the test can reach the service or account under test.");

    /// <summary>
    /// Returns the host from <paramref name="envVar"/>, or <paramref name="defaultHost"/>
    /// (localhost by default) when the variable is unset or empty.
    /// </summary>
    public static string GetHost(string envVar, string defaultHost = "localhost") => GetValue(envVar, defaultHost);

    /// <summary>
    /// Returns the port from <paramref name="envVar"/>, or <paramref name="defaultPort"/>
    /// when the variable is unset or not a valid integer.
    /// </summary>
    public static int GetPort(string envVar, int defaultPort)
        => int.TryParse(Environment.GetEnvironmentVariable(envVar), out var port) ? port : defaultPort;
}
