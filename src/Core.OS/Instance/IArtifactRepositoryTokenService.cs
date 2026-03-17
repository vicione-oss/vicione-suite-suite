using Core.OS.Instance.Contracts;

namespace Core.OS.Instance;

public interface IArtifactRepositoryTokenService
{
    Task<ArtifactRepositoryTokenResponse> GetToken(Uri tokenEndpoint, CancellationToken cancellationToken);
}
