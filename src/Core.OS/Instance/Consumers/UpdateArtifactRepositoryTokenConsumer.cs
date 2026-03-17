using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class UpdateArtifactRepositoryTokenConsumer(
    IArtifactRepositoryStore repositoryStore,
    IArtifactRepositoryTokenService tokenService,
    ILogger<UpdateArtifactRepositoryTokenConsumer> logger) : IConsumer<UpdateArtifactRepositoryToken>
{
    public async Task Consume(ConsumeContext<UpdateArtifactRepositoryToken> context)
    {
        LogConsumeUpdateRepositoryToken(logger, context.Message.CorrelationId, context.Message.Repository.Id);

        var repo = context.Message.Repository;
        var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Updated)
        {
            CorrelationId = context.Message.CorrelationId
        };

        try
        {
            if (string.IsNullOrWhiteSpace(repo.TokenEndpoint))
                throw new InvalidOperationException("Repository source has no token endpoint defined.");

            var tokenUri = new Uri(repo.TokenEndpoint);
            var tokenResponse = await tokenService.GetToken(tokenUri, context.CancellationToken);

            repo.Password = tokenResponse.Token;
            repo.TokenValidUntil = tokenResponse.ValidUntil;

            await repositoryStore.CreateOrUpdate(repo, context.CancellationToken);

            await context.Publish(changeEvent);
        }
        catch (Exception e)
        {
            LogFailedToUpdateRepositoryToken(logger, e, repo.Id);

            await context.Publish(changeEvent with { Error = new ErrorInfo(100, e.Message) });
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume UpdateArtifactRepositoryTokenConsumer CorrelationId:{correlationId} Source:{sourceId}")]
    private static partial void LogConsumeUpdateRepositoryToken(ILogger<UpdateArtifactRepositoryTokenConsumer> logger, Guid correlationId, Guid sourceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update repository source {id}")]
    private static partial void LogFailedToUpdateRepositoryToken(ILogger<UpdateArtifactRepositoryTokenConsumer> logger, Exception exception, Guid id);
}
