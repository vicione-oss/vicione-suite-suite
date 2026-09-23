namespace Core.Artifacts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter properties are required for deserialisation")]
public class ArtifactRepositoryOptions
{
    /// <summary>
    /// The configuration section name used in <c>appsettings.json</c>.
    /// </summary>
    public const string ConfigSection = "ArtifactRepository";

    public List<ArtifactRepositorySourceOption> Sources { get; set; } = [];

    /// <summary>
    /// Base64-encoded public keys used to verify artifact package signatures.
    /// </summary>
    public List<string> PublicKeys { get; set; } = [];

    /// <summary>
    /// Tokens older than this are refreshed on the next request, not on a timer.
    /// </summary>
    public long SourceTokenRefreshIntervalDays { get; set; } = 7;
}
