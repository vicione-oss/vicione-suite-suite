namespace Core.Artifacts;

/// <summary>
/// Represents a single artifact repository source configuration.
/// </summary>
public class ArtifactRepositorySourceOption
{
    /// <summary>
    /// Gets or sets the secure (HTTPS) base URI of the artifact repository endpoint.
    /// </summary>
    /// <example><c>https://system.update.release</c></example>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the username for authenticating with the repository, if required.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the password or token used for authenticating with the repository, if required.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets a secure (HTTPS) URI for obtaining an authentication token. 
    /// Is <see langword="null"/> when the <see cref="Password"/> is used directly for authentication.
    /// </summary>
    public string? TokenEndpoint { get; set; }
}

