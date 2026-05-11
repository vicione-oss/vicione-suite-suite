using Core.OS.Instance.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class CreateArtifactRepositoryConsumer(
    IArtifactRepositoryStore repositoryStore,
    IArtifactRepositoryTokenService tokenService,
    ILogger<CreateArtifactRepositoryConsumer> logger) : IConsumer<CreateArtifactRepository>
{
    public async Task Consume(ConsumeContext<CreateArtifactRepository> context)
    {
        var correlationId = context.Message.CorrelationId;
        var repository = context.Message.Repository;

        LogConsume(logger, correlationId, repository.Id);

        try
        {
            await tokenService.UpdateRepositoryToken(repository, context.CancellationToken);

            var result = await repositoryStore.CreateOrUpdate(repository);

            LogRepositoryCreated(logger, correlationId, repository.Id);

            var changeEvent = new ArtifactRepositoryChanged(repository, CrudAction.Created)
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogFailedToUpdateRepository(logger, e, correlationId, repository.Id);

            var changeEvent = new ArtifactRepositoryChanged(repository, CrudAction.Created, new ErrorInfo(100, e.Message))
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume create artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<CreateArtifactRepositoryConsumer> logger, Guid correlationId, Guid repositoryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogRepositoryCreated(ILogger<CreateArtifactRepositoryConsumer> logger, Guid correlationId, Guid repositoryId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create artifact repository='{RepositoryId}' correlated by {CorrelationId}")]
    private static partial void LogFailedToUpdateRepository(ILogger<CreateArtifactRepositoryConsumer> logger, Exception exception, Guid correlationId, Guid repositoryId);
}
