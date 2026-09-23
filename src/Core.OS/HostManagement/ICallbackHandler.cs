namespace Core.OS.HostManagement;

public interface ICallbackHandler
{
    /// <summary>
    ///     The unique topic to be handled
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Handles a message whose content is the JSON-serialized payload.
    /// </summary>
    Task Handle(string messageContent, CancellationToken cancellationToken);
}
