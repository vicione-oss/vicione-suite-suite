namespace Core.Module.Options;

/// <summary>
/// Represents a single artifact repository source configuration.
/// </summary>
public class ArtifactRepositorySource
{
    /// <summary>
    /// Gets or sets the base URI of the artifact repository endpoint.
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
}

