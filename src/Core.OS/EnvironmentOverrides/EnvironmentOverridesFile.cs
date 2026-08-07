using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Core.Module.Extensions;

namespace Core.OS.EnvironmentOverrides;

/// <summary>
/// Resolves the path of the runtime environment-variable override file and defines which keys
/// may be written to it.
/// Deliberately free of dependency injection and the configuration pipeline so the same
/// anchor can be used both at the very top of <c>Main</c> (before the host builder exists)
/// and by the repository that persists the file.
/// </summary>
internal static partial class EnvironmentOverridesFile
{
    /// <summary>
    /// Environment variable that switches the feature on. A deployment decides whether runtime
    /// overrides exist at all, not where they live: the suite can only count on its own data
    /// directory being writable, so there is nowhere else to point it at.
    /// </summary>
    public const string EnabledEnvironmentVariable = "VICIONE_SUITE_ENV_OVERRIDES";

    /// <summary>
    /// The file sits in the root of the instance home directory rather than in a module workspace
    /// below it, which is what keeps a reset and a restore from taking it: both clear the child
    /// directories. Overrides describe how this machine is wired up, not what the instance holds,
    /// so they are meant to outlive both — the recovery path stays deleting the file by hand.
    /// </summary>
    private const string FileName = "env-overrides.env";

    /// <param name="homeDirectory">
    /// <see cref="Instance.InstanceOptions.HomeDirectory"/> as configured for this instance.
    /// </param>
    /// <returns>The path of the override file, or <c>null</c> if the feature is switched off.</returns>
    public static string? ResolvePath(IFileSystem fileSystem, string? homeDirectory)
        => IsEnabled() && !string.IsNullOrWhiteSpace(homeDirectory)
            ? fileSystem.Path.Combine(fileSystem.GetRootedPath(homeDirectory), FileName)
            : null;

    /// <summary>
    /// The path for callers that cannot work without one. Reading or writing overrides while the
    /// feature is off is refused rather than answered with "no overrides", which would be
    /// indistinguishable from an empty file and imply that a save would stick.
    /// </summary>
    public static string RequirePath(IFileSystem fileSystem, string? homeDirectory)
        => ResolvePath(fileSystem, homeDirectory) ?? throw new InvalidOperationException(
            $"Runtime environment overrides are switched off: '{EnabledEnvironmentVariable}' is not enabled.");

    /// <summary>
    /// Authoritative check that a key may be written to the file: keys are written verbatim, so
    /// one containing a newline, quote or '=' can append further lines and set variables that
    /// were never submitted — bypassing any per-entry policy check and understating what was
    /// applied in the audit log. The pattern is shared with the UI via
    /// <see cref="Shared.EnvironmentOverrides.Constants.KeyPattern"/>.
    /// </summary>
    public static bool IsValidKey(string key) => ValidKey().IsMatch(key);

    /// <summary>
    /// Authoritative check that a value may be written to the file. Every other character survives
    /// the round trip because the repository escapes it; NUL cannot, because
    /// <see cref="Environment.SetEnvironmentVariable(string,string)"/> truncates the value there
    /// without reporting anything, so the applied override would differ from the stored one.
    /// </summary>
    public static bool IsValidValue(string value) => !value.Contains('\0');

    // "1" counts as on next to "true": the switch is typed into a systemd EnvironmentFile, where
    // both spellings are idiomatic. Anything else, an unset variable included, leaves it off.
    private static bool IsEnabled()
    {
        var configuredValue = Environment.GetEnvironmentVariable(EnabledEnvironmentVariable)?.Trim();

        return configuredValue == "1" || (bool.TryParse(configuredValue, out var enabled) && enabled);
    }

    [GeneratedRegex(Shared.EnvironmentOverrides.Constants.KeyPattern)]
    private static partial Regex ValidKey();
}
