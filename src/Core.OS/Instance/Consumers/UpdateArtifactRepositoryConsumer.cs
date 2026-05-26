using Core.OS.Instance.Extensions;
using Core.Shared.Instance.Commands;
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
        var correlationId = context.Message.CorrelationId;
        var repository = context.Message.Repository;

        LogConsume(logger, correlationId, repository.Id);

        try
        {
            await tokenService.UpdateRepositoryToken(repository, context.CancellationToken);

            var result = await repositoryStore.CreateOrUpdate(repository);

            LogRepositoryUpdated(logger, correlationId, repository.Id);

            var changeEvent = new ArtifactRepositoryChanged(repository, result)
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, repository.Id);

            var changeEvent = new ArtifactRepositoryChanged(repository, CrudAction.Updated, new ErrorInfo(100, e.Message))
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume update artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<UpdateArtifactRepositoryConsumer> logger, Guid correlationId, Guid? repositoryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogRepositoryUpdated(ILogger<UpdateArtifactRepositoryConsumer> logger, Guid correlationId, Guid repositoryId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<UpdateArtifactRepositoryConsumer> logger, Exception exception, Guid correlationId, Guid repositoryId);
}
