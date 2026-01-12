namespace Core.OS.HostManagement;

public interface ICallbackHandler
{
    /// <summary>
    ///     The unique topic to be handled
    /// </summary>
    string Topic { get; }

    /// <summary>
    ///     The method handling a message
    /// </summary>
    /// <param name="messageContent">Json-serialized message content</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    Task Handle(string messageContent, CancellationToken cancellationToken);
}
