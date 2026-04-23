namespace Core.Artifacts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter properties are required for deserialisation")]
/// <summary>
/// Represents the configuration options for the artifact repository API.
/// </summary>
public class ArtifactRepositoryOptions
{
    /// <summary>
    /// The configuration section name used in <c>appsettings.json</c>.
    /// </summary>
    public const string ConfigSection = "ArtifactRepository";

    /// <summary>
    /// Gets or sets the list of repository sources where artifacts are retrieved from.
    /// </summary>
    public List<ArtifactRepositorySourceOption> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of public keys (base64 encoded) used to verify the digital signatures
    /// of artifact packages when interacting with <see cref="Sdk.Backend.Artifacts.IArtifactRepository"/>.
    /// </summary>
    public List<string> PublicKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the lifetime of the artifact package cache, in milliseconds.
    /// After this period, the cache is considered invalid and will be refreshed upon the next request.
    /// </summary>
    /// <remarks>Default is 300,000 ms (5 minutes).</remarks>
    public long PackageCacheLifetimeMs { get; set; } = 300_000;

    /// <summary>
    /// Gets or set the interval in days for refreshing the source tokens. 
    /// After this period, the tokens will be refreshed upon the next request.
    /// </summary>
    public long SourceTokenRefreshIntervalDays { get; set; } = 7;
}
