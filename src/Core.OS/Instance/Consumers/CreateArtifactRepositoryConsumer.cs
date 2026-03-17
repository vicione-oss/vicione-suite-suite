using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class CreateArtifactRepositoryConsumer(
    IArtifactRepositoryStore repositoryStore,
    ILogger<CreateArtifactRepositoryConsumer> logger) : IConsumer<CreateArtifactRepository>
{
    public async Task Consume(ConsumeContext<CreateArtifactRepository> context)
    {
        LogConsumeCreateRepository(logger, context.Message.CorrelationId, context.Message.Repository.Id);

        var repo = context.Message.Repository;
        var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Created)
        {
            CorrelationId = context.Message.CorrelationId
        };

        try
        {
            var result = await repositoryStore.CreateOrUpdate(repo);


            await context.Publish(changeEvent);
        }
        catch (Exception e)
        {
            LogFailedToUpdateRepository(logger, e, repo.Id);

            await context.Publish(changeEvent with { Error = new ErrorInfo(100, e.Message) });
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume CreateArtifactRepositoryConsumer CorrelationId:{correlationId} Source:{sourceId}")]
    private static partial void LogConsumeCreateRepository(ILogger<CreateArtifactRepositoryConsumer> logger, Guid correlationId, Guid sourceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update repository source {id}")]
    private static partial void LogFailedToUpdateRepository(ILogger<CreateArtifactRepositoryConsumer> logger, Exception exception, Guid id);
}
