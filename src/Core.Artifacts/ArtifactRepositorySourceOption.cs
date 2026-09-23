namespace Core.Artifacts;

public class ArtifactRepositorySourceOption
{
    /// <summary>
    /// Base URI of the repository endpoint; must be HTTPS, e.g. <c>https://system.update.release</c>.
    /// </summary>
    public required string Endpoint { get; set; }

    public string? UserName { get; set; }

    /// <summary>
    /// Password or access token for the repository.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// HTTPS URI for obtaining an auth token; <see langword="null"/> when <see cref="Password"/> is used directly.
    /// </summary>
    public string? TokenEndpoint { get; set; }
}

