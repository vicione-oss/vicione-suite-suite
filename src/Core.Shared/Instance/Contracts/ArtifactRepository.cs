namespace Core.Shared.Instance.Contracts;

public class ArtifactRepository
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Base URI of the repository endpoint, e.g. <c>https://system.update.release</c>.
    /// </summary>
    public required string Endpoint { get; set; }

    public string? Name { get; set; }

    public string? UserName { get; set; }

    /// <summary>
    /// Password or access token for the repository.
    /// </summary>
    public string? Password { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset? Modified { get; set; }

    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Endpoint used to refresh token-based authentication.
    /// </summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>
    /// Token expiry; drives when token-based authentication is refreshed.
    /// </summary>
    public DateTimeOffset? TokenValidUntil { get; set; }
}
