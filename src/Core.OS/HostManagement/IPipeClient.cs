using HostManagement.Shared.Communication.Enums;

namespace Core.OS.HostManagement;

public interface IPipeClient : IDisposable
{
    /// <summary>
    /// State of the pipe.
    /// </summary>
    PipeState State { get; }

    /// <summary>
    /// Waits for a client to connect to the pipe.
    /// </summary>
    Task Connect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the pipe.
    /// </summary>
    Task<string> SendRequest(string topic, string content, CancellationToken cancellationToken = default);
}
