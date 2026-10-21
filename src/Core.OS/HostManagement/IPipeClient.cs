using Core.Shared.HostManagement;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Communication.NamedPipe.Client;

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
    /// <param name="request">One of <see cref="PipeRequests"/> or <see cref="CapabilityPipeRequests"/>, which selects the request handler.</param>
    /// <param name="content">The payload the handler of <paramref name="request"/> expects, empty for requests that need none.</param>
    /// <param name="cancellationToken">Cancels waiting for the response.</param>
    /// <exception cref="HostManagementRequestException">Thrown when HostManagement does not know the topic, has no handler for it, or the handler threw or was canceled.</exception>
    Task<TResponse> SendRequest<TResponse>(PipeRequest<TResponse> request, string content = "", CancellationToken cancellationToken = default);
}
