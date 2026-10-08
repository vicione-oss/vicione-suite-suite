using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.NamedPipe.Client;

namespace Core.OS.HostManagement.Handlers;

/// <summary>
/// Drops the cached system configuration when HostManagement reports a change made outside the Suite.
/// </summary>
public sealed partial class SystemConfigurationChangedCallbackHandler(
    SystemConfigurationCache cache,
    ILogger<SystemConfigurationChangedCallbackHandler> logger) : ICallbackHandler<GetSystemConfigurationResult>, IPipeEventSubscriber
{
    /// <inheritdoc/>
    public void RegisterWith(CallbackHandlerRegistry registry)
        => registry.RegisterHandler(PipeEvents.SystemConfigurationChanged, this);

    /// <inheritdoc/>
    public Task HandleAsync(GetSystemConfigurationResult message, Guid correlationId, CancellationToken cancellationToken)
    {
        LogSystemConfigurationChanged(logger);

        cache.Invalidate();

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HostManagement reported a system configuration change, invalidating cache.")]
    private static partial void LogSystemConfigurationChanged(ILogger<SystemConfigurationChangedCallbackHandler> logger);
}
