namespace Core.OS.Hosting.Contracts;

public sealed record FallbackVersionDiagnostics
{
    /// <summary>
    /// The deployed <c>version.json</c> string, which falls back to the running assembly version
    /// when the file is missing or unreadable.
    /// </summary>
    public required string LocalVersion { get; init; }

    /// <summary>The version of the SDK this suite is built against.</summary>
    public required string SdkVersion { get; init; }

    public string? BranchName { get; init; }

    /// <summary>
    /// The version the persisted data was written by, or <see langword="null"/> if the file is
    /// missing or the home directory could not be resolved.
    /// </summary>
    public string? DataVersion { get; init; }
}
