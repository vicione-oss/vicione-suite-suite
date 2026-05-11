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
        var correlationId = context.Message.CorrelationId;
        var repository = context.Message.Repository;

        LogConsume(logger, correlationId, repository.Id);

        try
        {
            if (string.IsNullOrWhiteSpace(repository.TokenEndpoint))
                throw new InvalidOperationException("Repository source has no token endpoint defined.");

            var tokenUri = new Uri(repository.TokenEndpoint);
            var tokenResponse = await tokenService.GetToken(tokenUri, context.CancellationToken);

            repository.Password = tokenResponse.Token;
            repository.TokenValidUntil = tokenResponse.ValidUntil;

            await repositoryStore.CreateOrUpdate(repository, context.CancellationToken);

            LogRepositoryTokenUpdated(logger, correlationId, repository.Id);

            var changeEvent = new ArtifactRepositoryChanged(repository, CrudAction.Updated)
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume update artifact repository='{RepositoryId}' token correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<UpdateArtifactRepositoryTokenConsumer> logger, Guid correlationId, Guid repositoryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated artifact repository='{RepositoryId}' token correlated by {CorrelationId}")]
    private static partial void LogRepositoryTokenUpdated(ILogger<UpdateArtifactRepositoryTokenConsumer> logger, Guid correlationId, Guid repositoryId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update artifact repository='{RepositoryId}' token correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<UpdateArtifactRepositoryTokenConsumer> logger, Exception exception, Guid correlationId, Guid repositoryId);
}
