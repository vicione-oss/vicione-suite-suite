using Core.OS.Instance.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class UpdateArtifactRepositoryConsumer(
    IArtifactRepositoryStore repositoryStore,
    IArtifactRepositoryTokenService tokenService,
    ILogger<UpdateArtifactRepositoryConsumer> logger) : IConsumer<UpdateArtifactRepository>
{
    public async Task Consume(ConsumeContext<UpdateArtifactRepository> context)
    {
        LogConsumeUpdateArtifactRepository(logger, context.Message.CorrelationId, context.Message.Repository.Id);

        var repo = context.Message.Repository;

        try
        {
            await tokenService.UpdateRepositoryToken(repo, context.CancellationToken);

            var result = await repositoryStore.CreateOrUpdate(repo);

            var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Updated)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogFailedToUpdateRepository(logger, e, repo.Id);

            var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Updated, new ErrorInfo(100, e.Message))
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume UpdateArtifactRepositoryConsumer CorrelationId:{correlationId} Repository:{sourceId}")]
    private static partial void LogConsumeUpdateArtifactRepository(ILogger<UpdateArtifactRepositoryConsumer> logger, Guid correlationId, Guid sourceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update repository {id}")]
    private static partial void LogFailedToUpdateRepository(ILogger<UpdateArtifactRepositoryConsumer> logger, Exception exception, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch token for repository {id}")]
    private static partial void LogFailedToGetToken(ILogger<UpdateArtifactRepositoryConsumer> logger, Exception exception, Guid id);
}
