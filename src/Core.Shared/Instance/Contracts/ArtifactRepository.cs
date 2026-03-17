namespace Core.Shared.Instance.Contracts;

public class ArtifactRepository
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the base URI of the artifact repository endpoint.
    /// </summary>
    /// <example><c>https://system.update.release</c></example>
    public required string Endpoint { get; set; }

    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the username for authenticating with the repository, if required.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the password or token used for authenticating with the repository, if required.
    /// </summary>
    public string? Password { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset? Modified { get; set; }

    public string? ModifiedBy { get; set; }

    /// <summary>
    /// An optional endpoint to refresh the token based authentication
    /// </summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>
    /// Optional expiration time of the token, used to determine when to refresh the token based authentication
    /// </summary>
    public DateTimeOffset? TokenValidUntil { get; set; }
}
