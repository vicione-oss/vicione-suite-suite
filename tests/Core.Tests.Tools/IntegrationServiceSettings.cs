namespace Core.Tests.Tools;

/// <summary>
/// Resolves connection details for external services used by integration tests
/// (mail, databases, brokers, ...). Values are read from environment variables so
/// that CI can point tests at a service it provides, while falling back to
/// localhost defaults suitable for a service started locally (e.g. mailpit via
/// docker or mise). See docs/integration-testing.md.
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
    /// Returns the host from <paramref name="envVar"/>, or <paramref name="defaultHost"/>
    /// (localhost by default) when the variable is unset or empty.
    /// </summary>
    public static string GetHost(string envVar, string defaultHost = "localhost")
        => GetValue(envVar, defaultHost);

    /// <summary>
    /// Returns the port from <paramref name="envVar"/>, or <paramref name="defaultPort"/>
    /// when the variable is unset or not a valid integer.
    /// </summary>
    public static int GetPort(string envVar, int defaultPort)
        => int.TryParse(Environment.GetEnvironmentVariable(envVar), out var port) ? port : defaultPort;
}
