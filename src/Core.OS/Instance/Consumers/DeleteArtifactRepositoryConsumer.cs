using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class DeleteArtifactRepositoryConsumer(
    IArtifactRepositoryStore repositoryStore,
    ILogger<DeleteArtifactRepositoryConsumer> logger) : IConsumer<DeleteArtifactRepository>
{
    public async Task Consume(ConsumeContext<DeleteArtifactRepository> context)
    {
        LogConsumeDeleteRepository(logger, context.Message.CorrelationId, context.Message.RepositoryId);

        try
        {
            var deleted = await repositoryStore.Delete([context.Message.RepositoryId], context.CancellationToken);
            if (deleted is null || !deleted.Any(k => k.Id == context.Message.RepositoryId))
                throw new InvalidOperationException($"Repository with id {context.Message.RepositoryId} not found");

            var changeEvent = new ArtifactRepositoryChanged(deleted.First(), CrudAction.Deleted)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogFailedToDeleteRepository(logger, e);

            var repo = new ArtifactRepository() { Id = context.Message.RepositoryId, Endpoint = "null" };
            var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Deleted, new ErrorInfo(100, e.Message))
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume DeleteArtifactRepository CorrelationId:{correlationId} Id:{repositoryId}")]
    private static partial void LogConsumeDeleteRepository(ILogger<DeleteArtifactRepositoryConsumer> logger, Guid correlationId, Guid repositoryId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update repository sources")]
    private static partial void LogFailedToDeleteRepository(ILogger<DeleteArtifactRepositoryConsumer> logger, Exception exception);
}
