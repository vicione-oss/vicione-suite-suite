namespace Core.Shared.EnvironmentOverrides;

/// <summary>
/// The switch that decides whether runtime environment overrides exist on an instance at all.
/// Read by the startup loader, which applies the file, and by the settings panel, which decides
/// whether to offer itself — one switch, so a panel is never offered on an instance that would
/// not apply what it stores.
/// </summary>
public static class EnvironmentOverridesSwitch
{
    /// <summary>
    /// A deployment decides whether overrides exist, not where they live: the suite can only count
    /// on its own data directory being writable, so there is nowhere else to point it at.
    /// </summary>
    /// <remarks>
    /// An environment variable rather than a feature flag, for two reasons. The loader runs at the
    /// top of <c>Main</c>, where no feature manager exists yet. And this is the only switch the
    /// override file cannot flip: entries reach the process environment before configuration is
    /// read, so a file could set a flag that gates itself, while it can never switch on the
    /// mechanism that reads it in the first place.
    /// </remarks>
    public const string EnabledEnvironmentVariable = "VICIONE_SUITE_ENV_OVERRIDES";

    // "1" counts as on next to "true": the switch is typed into a systemd EnvironmentFile, where
    // both spellings are idiomatic. Anything else, an unset variable included, leaves it off.
    public static bool IsEnabled()
    {
        var configuredValue = Environment.GetEnvironmentVariable(EnabledEnvironmentVariable)?.Trim();

        return configuredValue == "1" || (bool.TryParse(configuredValue, out var enabled) && enabled);
    }
}
