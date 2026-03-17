using Sdk.Messaging;

namespace Core.Shared.Instance.Requests;

public sealed record GetArtifactRepositories(List<Guid>? RepositoryIds = null) : IRequest<GetArtifactRepositoriesResponse>;
