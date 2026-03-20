using Core.Shared.Instance.Contracts;

namespace Core.OS.Instance.Extensions;

internal static class IArtifactRepositoryTokenServiceExtensions
{
    internal static async Task UpdateRepositoryToken(this IArtifactRepositoryTokenService tokenService, ArtifactRepository repo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(repo.TokenEndpoint))
            return;

        try
        {
            // if token endpoint is set, we assume it's a new token and update the token updated time to now
            var tokenUri = new Uri(repo.TokenEndpoint);
            var token = await tokenService.GetToken(tokenUri, cancellationToken); // this will update the token updated time

            repo.Password = token.Token;
            repo.TokenValidUntil = token.ValidUntil;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Invalid {nameof(repo.TokenEndpoint)} - failed to retrieve a valid token for repo '{repo.Name}'.", e);
        }
    }
}
