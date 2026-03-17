using Core.Shared.Instance.Requests;
using MassTransit;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class GetArtifactRepositoriesConsumer(IArtifactRepositoryStore repositoryStore, ILogger<GetArtifactRepositoriesConsumer> logger) : RequestConsumer<GetArtifactRepositories, GetArtifactRepositoriesResponse>
{
    protected override async Task<GetArtifactRepositoriesResponse> Respond(ConsumeContext<GetArtifactRepositories> context)
    {
        var repos = await repositoryStore.GetRepositories(context.Message.RepositoryIds, context.CancellationToken);

        return new GetArtifactRepositoriesResponse(repos);
    }

    protected override Task<GetArtifactRepositoriesResponse> HandleException(ConsumeContext<GetArtifactRepositories> context, Exception e)
    {
        LogFailedToHandleGetRepositories(logger, e);

        return Task.FromResult(new GetArtifactRepositoriesResponse([], new(0, e.Message)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to handle GetArtifactRepositories")]
    private static partial void LogFailedToHandleGetRepositories(ILogger<GetArtifactRepositoriesConsumer> logger, Exception exception);
}
