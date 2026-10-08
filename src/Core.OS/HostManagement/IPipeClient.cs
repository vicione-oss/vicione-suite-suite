using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;

namespace Core.OS.HostManagement;

public interface IPipeClient : IAsyncDisposable
{
    PipeState State { get; }

    /// <summary>
    /// Whether the client is a stand-in used where host management is not installed.
    /// Says nothing about reachability, see <see cref="State"/>.
    /// </summary>
    bool IsMock { get; }

    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not established within <see cref="HostManagementOptions.ConnectTimeoutMs"/>.
    /// </exception>
    Task Connect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request and waits for its response, connecting first as <see cref="Connect"/> does if the pipe is not connected yet.
    /// </summary>
    /// <param name="topic">One of <see cref="Topics"/>, which selects the request handler.</param>
    /// <param name="content">The payload the handler of <paramref name="topic"/> expects, empty for topics that need none.</param>
    /// <param name="cancellationToken">Cancels waiting for the response.</param>
    /// <returns>The content of the response message.</returns>
    Task<string> SendRequest(string topic, string content, CancellationToken cancellationToken = default);
}
