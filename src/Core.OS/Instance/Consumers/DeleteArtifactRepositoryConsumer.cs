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
        var correlationId = context.Message.CorrelationId;
        var repositoryId = context.Message.RepositoryId;

        LogConsumeDeleteRepository(logger, correlationId, repositoryId);

        try
        {
            var deleted = await repositoryStore.Delete([repositoryId], context.CancellationToken);
            if (deleted is null || !deleted.Any(k => k.Id == repositoryId))
                throw new InvalidOperationException($"Repository with id {repositoryId} not found");

            LogRepositoryDeleted(logger, correlationId, repositoryId);

            var changeEvent = new ArtifactRepositoryChanged(deleted.First(), CrudAction.Deleted)
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, repositoryId);

            var repo = new ArtifactRepository() { Id = repositoryId, Endpoint = "null" };
            var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Deleted, new ErrorInfo(100, e.Message))
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume delete artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogConsumeDeleteRepository(ILogger<DeleteArtifactRepositoryConsumer> logger, Guid correlationId, Guid repositoryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogRepositoryDeleted(ILogger<DeleteArtifactRepositoryConsumer> logger, Guid correlationId, Guid repositoryId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<DeleteArtifactRepositoryConsumer> logger, Exception exception, Guid correlationId, Guid repositoryId);
}
