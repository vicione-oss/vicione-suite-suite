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
        LogConsumeCreateRepository(logger, context.Message.CorrelationId, context.Message.Repository.Id);

        var repo = context.Message.Repository;

        try
        {
            await tokenService.UpdateRepositoryToken(repo, context.CancellationToken);

            var result = await repositoryStore.CreateOrUpdate(repo);

            var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Created)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception e)
        {
            LogFailedToUpdateRepository(logger, e, repo.Id);

            var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Created, new ErrorInfo(100, e.Message))
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume CreateArtifactRepositoryConsumer CorrelationId:{correlationId} Source:{sourceId}")]
    private static partial void LogConsumeCreateRepository(ILogger<CreateArtifactRepositoryConsumer> logger, Guid correlationId, Guid sourceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update repository source {id}")]
    private static partial void LogFailedToUpdateRepository(ILogger<CreateArtifactRepositoryConsumer> logger, Exception exception, Guid id);
}
