using HostManagement.Shared.Communication.Enums;

namespace Core.OS.HostManagement;

public interface IPipeClient : IDisposable
{
    /// <summary>
    /// Gets the state of the pipe.
    /// </summary>
    PipeState State { get; }

    /// <summary>
    /// Waits for a client to connect to the pipe.
    /// </summary>
    /// <remarks>
    /// Throws exceptions when something fails.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    Task Connect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the pipe.
    /// </summary>
    Task<string> SendRequest(string topic, string content, CancellationToken cancellationToken = default);
}
