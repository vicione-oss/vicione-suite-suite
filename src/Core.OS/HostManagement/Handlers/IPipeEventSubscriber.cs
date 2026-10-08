using HostManagement.Shared.Communication.NamedPipe.Client;

namespace Core.OS.HostManagement.Handlers;

/// <summary>
/// Subscribes to a HostManagement pipe event topic.
/// </summary>
public interface IPipeEventSubscriber
{
    /// <summary>
    /// Registers the handler for the subscribed topic.
    /// </summary>
    /// <param name="registry">The registry the pipe client dispatches events with.</param>
    void RegisterWith(CallbackHandlerRegistry registry);
}
