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
        var changeEvent = new ArtifactRepositoryChanged(repo, CrudAction.Updated)
        {
            CorrelationId = context.Message.CorrelationId
        };

        try
        {
            await UpdateRepositoryToken(repo, context.CancellationToken);

            var result = await repositoryStore.CreateOrUpdate(repo);

            await context.Publish(changeEvent);
        }
        catch (Exception e)
        {
            LogFailedToUpdateRepository(logger, e, repo.Id);

            await context.Publish(changeEvent with { Error = new ErrorInfo(100, e.Message) });
        }
    }

    private async Task UpdateRepositoryToken(ArtifactRepository repo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(repo.TokenEndpoint))
            return;

        try
        {
            // if token endpoint is set, we assume it's a new token and update the token updated time to now
            var tokenUri = new Uri(repo.TokenEndpoint);
            var token = await tokenService.GetToken(tokenUri, cancellationToken); // this will update the token updated time

            repo.Password = token.Token;
            repo.TokenValidUntil = token.ValidUntil;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Invalid {nameof(repo.TokenEndpoint)} - failed to retrieve a valid token", e);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume UpdateArtifactRepositoryConsumer CorrelationId:{correlationId} Repository:{sourceId}")]
    private static partial void LogConsumeUpdateArtifactRepository(ILogger<UpdateArtifactRepositoryConsumer> logger, Guid correlationId, Guid sourceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update repository {id}")]
    private static partial void LogFailedToUpdateRepository(ILogger<UpdateArtifactRepositoryConsumer> logger, Exception exception, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch token for repository {id}")]
    private static partial void LogFailedToGetToken(ILogger<UpdateArtifactRepositoryConsumer> logger, Exception exception, Guid id);
}
