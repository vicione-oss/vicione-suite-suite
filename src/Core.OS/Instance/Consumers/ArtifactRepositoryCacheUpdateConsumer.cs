using Core.OS.Modules;
using Core.Shared.Instance.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class ArtifactRepositoryChangeConsumer(
    IArtifactRepositoryStore repositoryStore,
    IArtifactRepositoryOptionsCache optionsCache,
    IArtifactRepositoryTokenService tokenService,
    IModuleArtifactCache artifactCache,
    ILogger<ArtifactRepositoryChangeConsumer> logger) :
    IConsumer<ArtifactRepositoryChanged>
{
    public async Task Consume(ConsumeContext<ArtifactRepositoryChanged> context)
        => await TryInvalidateCaches(context.Message.Repository.Name, context.Message.Action, context.Message.Error, context.CancellationToken);

    private async Task TryInvalidateCaches(string? source, CrudAction action, ErrorInfo? error, CancellationToken cancellationToken)
    {
        if (error is not null)
            return;

        try
        {
            await artifactCache.Invalidate(cancellationToken);

            LogInvalidatedArtifacts(logger, action, source);
        }
        catch (Exception ex)
        {
            LogFailedToInvalidateArtifacts(logger, ex, action, source);
        }

        try
        {
            await optionsCache.ReloadOptions(repositoryStore, tokenService, cancellationToken);

            if (logger.IsEnabled(LogLevel.Information))
            {
                LogReloadedOptions(logger, action, source, optionsCache.GetOptions().Sources.Count);
            }
        }
        catch (Exception ex)
        {
            LogFailedToReloadOptions(logger, ex, action, source);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloaded {SourcesCount} repository options on change='{Action}' of source='{Source}'")]
    private static partial void LogReloadedOptions(ILogger<ArtifactRepositoryChangeConsumer> logger, CrudAction action, string? source, int sourcesCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to invalidate repository options on change='{Action}' of source='{Source}'")]
    private static partial void LogFailedToReloadOptions(ILogger<ArtifactRepositoryChangeConsumer> logger, Exception ex, CrudAction action, string? source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Invalidate artifact cache on change='{Action}' of source='{Source}'")]
    private static partial void LogInvalidatedArtifacts(ILogger<ArtifactRepositoryChangeConsumer> logger, CrudAction action, string? source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to invalidate artifact cache on change='{Action}' of source='{Source}'")]
    private static partial void LogFailedToInvalidateArtifacts(ILogger<ArtifactRepositoryChangeConsumer> logger, Exception ex, CrudAction action, string? source);
}
