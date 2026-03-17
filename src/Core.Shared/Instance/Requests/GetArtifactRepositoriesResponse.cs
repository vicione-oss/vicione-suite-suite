using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Instance.Requests;

public sealed record GetArtifactRepositoriesResponse(List<ArtifactRepository> Repositories, ErrorInfo? RequestError = null) : IResponse;
