namespace Core.OS.Hosting.Contracts;

/// <summary>
/// The runtime environment-variable override file, which outlives every restart, reset and restore
/// and is therefore the one input an operator may have to take away to get the suite to boot.
/// Values are never carried here: the surface is unauthenticated and override values are where
/// secrets land, which is also why the stored-overrides log line records keys only.
/// </summary>
public sealed record FallbackEnvironmentOverridesDiagnostics
{
    /// <summary>Whether the deployment switched runtime overrides on at all.</summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// The resolved file path, or <see langword="null"/> when overrides are switched off or the
    /// home directory could not be resolved.
    /// </summary>
    public string? Path { get; init; }

    public bool FileExists { get; init; }

    /// <summary>
    /// Whether the page may offer the action that renames the file aside. False when there is no
    /// file to rename, or when the host was given no host-management options to restart through.
    /// </summary>
    public bool CanDisable { get; init; }

    /// <summary>The names of the overrides the file sets, in the order the file lists them.</summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>
    /// Why the file could not be read, if it could not be. A malformed file is already ignored at
    /// startup, so this says the overrides are not the reason the suite is in failsafe mode.
    /// </summary>
    public string? Error { get; init; }
}
