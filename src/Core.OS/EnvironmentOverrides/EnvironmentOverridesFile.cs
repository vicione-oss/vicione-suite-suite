using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Core.Module.Extensions;
using Core.Shared.EnvironmentOverrides;

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
    /// The file sits in the root of the instance home directory rather than in a module workspace
    /// below it, which is what keeps a reset and a restore from taking it: both clear the child
    /// directories. Overrides describe how this machine is wired up, not what the instance holds,
    /// so they are meant to outlive both — the recovery path is taking the file out of the picture,
    /// either by hand or through the failsafe debug page (see <see cref="ResolveDisabledPath"/>).
    /// </summary>
    private const string FileName = "env-overrides.env";

    /// <summary>
    /// The name the file is renamed to when an operator disables it from the failsafe debug page.
    /// One fixed name rather than a timestamped one, so repeated failures cannot pile up files, and
    /// a rename rather than a delete, so what broke the boot stays readable. Nothing resolves this
    /// name for reading, which is what makes the renamed file inert on the next boot.
    /// </summary>
    private const string DisabledFileName = FileName + ".disabled";

    /// <param name="homeDirectory">
    /// <see cref="Instance.InstanceOptions.HomeDirectory"/> as configured for this instance.
    /// </param>
    /// <returns>The path of the override file, or <c>null</c> if the feature is switched off.</returns>
    public static string? ResolvePath(IFileSystem fileSystem, string? homeDirectory)
        => Resolve(fileSystem, homeDirectory, FileName);

    /// <summary>
    /// Where <see cref="ResolvePath"/>'s file is moved to when an operator disables the overrides.
    /// </summary>
    public static string? ResolveDisabledPath(IFileSystem fileSystem, string? homeDirectory)
        => Resolve(fileSystem, homeDirectory, DisabledFileName);

    private static string? Resolve(IFileSystem fileSystem, string? homeDirectory, string fileName)
        => EnvironmentOverridesSwitch.IsEnabled() && !string.IsNullOrWhiteSpace(homeDirectory)
            ? fileSystem.Path.Combine(fileSystem.GetRootedPath(homeDirectory), fileName)
            : null;

    /// <summary>
    /// The path for callers that cannot work without one. Reading or writing overrides while the
    /// feature is off is refused rather than answered with "no overrides", which would be
    /// indistinguishable from an empty file and imply that a save would stick.
    /// </summary>
    public static string RequirePath(IFileSystem fileSystem, string? homeDirectory)
        => ResolvePath(fileSystem, homeDirectory) ?? throw new InvalidOperationException(
            "Runtime environment overrides are switched off: "
            + $"'{EnvironmentOverridesSwitch.EnabledEnvironmentVariable}' is not enabled.");

    /// <summary>
    /// Moves the override file to <see cref="ResolveDisabledPath"/>, which is what takes it out of
    /// the picture on the next boot. Overwrites a disabled file left by an earlier attempt: the
    /// current overrides are the ones that broke this boot, so they are the ones worth keeping
    /// around.
    /// </summary>
    /// <returns>The path the file was moved to.</returns>
    public static string Disable(IFileSystem fileSystem, string? homeDirectory)
    {
        var path = RequirePath(fileSystem, homeDirectory);
        var disabledPath = ResolveDisabledPath(fileSystem, homeDirectory)!;

        fileSystem.File.Move(path, disabledPath, overwrite: true);

        return disabledPath;
    }

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

    [GeneratedRegex(Shared.EnvironmentOverrides.Constants.KeyPattern)]
    private static partial Regex ValidKey();
}
