using HostManagement.Shared.Communication.Contracts;

namespace Core.OS.HostManagement;

public class EventCallbackRegistry(IEnumerable<ICallbackHandler> callbackHandlers, ILogger<EventCallbackRegistry> logger)
{
    private readonly Dictionary<string, ICallbackHandler> _handlers = callbackHandlers.ToDictionary(c => c.Topic);
    private readonly ILogger<EventCallbackRegistry> _logger = logger;

    public async Task Handle(NamedPipeMessage message, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(message.Topic, out var handler))
        {
            _logger.LogWarning("No handler registered for topic {MessageTopic}", message.Topic);
            return;
        }

        await handler.Handle(message.Content, cancellationToken).ConfigureAwait(false);
    }
}
