using Core.Shared.Instance.Requests;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class GetArtifactRepositoriesConsumer(IArtifactRepositoryStore repositoryStore, ILogger<GetArtifactRepositoriesConsumer> logger) : RequestConsumer<GetArtifactRepositories, GetArtifactRepositoriesResponse>
{
    public override async Task<GetArtifactRepositoriesResponse> Respond(GetArtifactRepositories message, CancellationToken cancellationToken)
    {
        var repos = await repositoryStore.GetRepositories(message.RepositoryIds, cancellationToken);

        return new GetArtifactRepositoriesResponse(repos);
    }

    public override Task<GetArtifactRepositoriesResponse> HandleException(GetArtifactRepositories message, Exception e, CancellationToken cancellationToken)
    {
        LogFailedToHandleGetRepositories(logger, e);

        return Task.FromResult(new GetArtifactRepositoriesResponse([], new(0, e.Message)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to handle GetArtifactRepositories")]
    private static partial void LogFailedToHandleGetRepositories(ILogger<GetArtifactRepositoriesConsumer> logger, Exception exception);
}
