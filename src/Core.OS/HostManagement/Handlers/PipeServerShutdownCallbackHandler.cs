using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.NamedPipe.Client;

namespace Core.OS.HostManagement.Handlers;

/// <summary>
/// Logs that HostManagement announced the shutdown of its pipe server.
/// </summary>
public sealed partial class PipeServerShutdownCallbackHandler(
    ILogger<PipeServerShutdownCallbackHandler> logger) : IPipeEventSubscriber
{
    /// <inheritdoc/>
    public void RegisterWith(CallbackHandlerRegistry registry)
        => registry.RegisterRawHandler(EventTopics.PipeServerShutdown, Handle);

    private Task Handle(string reason, Guid correlationId, CancellationToken cancellationToken)
    {
        LogPipeServerShutdown(logger, reason);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HostManagement announced the shutdown of its pipe server. Reason: {Reason}")]
    private static partial void LogPipeServerShutdown(ILogger<PipeServerShutdownCallbackHandler> logger, string reason);
}
