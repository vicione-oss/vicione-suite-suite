using Core.OS.Modules;
using Core.Shared.Instance.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class ArtifactRepositoryChangeConsumer(
    IArtifactRepositoryStore repositoryStore,
    IArtifactRepositoryOptionsCache optionsCache,
    IModuleArtifactCache artifactCache,
    ILogger<ArtifactRepositoryChangeConsumer> logger) :
    IConsumer<ArtifactRepositoryChanged>
{
    public async Task Consume(ConsumeContext<ArtifactRepositoryChanged> context)
        => await TryInvalidateCaches(context.Message.Action, context.Message.Error, context.CancellationToken);

    private async Task TryInvalidateCaches(CrudAction action, ErrorInfo? error, CancellationToken cancellationToken)
    {
        if (error is not null)
            return;

        try
        {
            await artifactCache.Invalidate(cancellationToken);
        }
        catch (Exception ex)
        {
            LogFailedToInvalidateArtifacts(logger, ex, action);
        }

        try
        {
            await optionsCache.ReloadOptions(repositoryStore, cancellationToken);
        }
        catch (Exception ex)
        {
            LogFailedToReloadOptions(logger, ex, action);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to invalidate repository options on change={action}")]
    private static partial void LogFailedToReloadOptions(ILogger<ArtifactRepositoryChangeConsumer> logger, Exception ex, CrudAction action);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to invalidate artifact cache {action}")]
    private static partial void LogFailedToInvalidateArtifacts(ILogger<ArtifactRepositoryChangeConsumer> logger, Exception ex, CrudAction action);
}
