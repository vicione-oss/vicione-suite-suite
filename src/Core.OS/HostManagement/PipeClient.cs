using Core.Shared.HostManagement;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Communication.NamedPipe.Client;
using Microsoft.Extensions.Options;
using HostManagementPipeClient = HostManagement.Shared.Communication.NamedPipe.Client.PipeClient;

namespace Core.OS.HostManagement;

/// <summary>
/// Adapts the HostManagement <see cref="HostManagementPipeClient"/> to the suite's <see cref="IPipeClient"/>.
/// </summary>
public sealed class PipeClient(
    IOptions<HostManagementOptions> options,
    ILogger<HostManagementPipeClient> logger,
    CallbackHandlerRegistry callbackHandlerRegistry) : IPipeClient
{
    private readonly HostManagementPipeClient _pipeClient = new(
        options.Value.PipeName,
        logger,
        callbackHandlerRegistry,
        options.Value.PipeOptions);

    /// <inheritdoc/>
    public PipeState State => _pipeClient.State;

    /// <inheritdoc/>
    public bool IsMock => false;

    /// <inheritdoc/>
    public async Task Connect(CancellationToken cancellationToken = default)
    {
        if (await _pipeClient.TryConnectAsync(options.Value.ConnectTimeoutMs, cancellationToken).ConfigureAwait(false))
            return;

        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException($"Failed to connect to pipe '{options.Value.PipeName}' within {options.Value.ConnectTimeoutMs} ms.");
    }

    /// <inheritdoc/>
    public async Task<TResponse> SendRequest<TResponse>(PipeRequest<TResponse> request, string content = "", CancellationToken cancellationToken = default)
    {
        // The HostManagement client connects without a time limit, so a stopped server would block the request until it is cancelled.
        if (State is not PipeState.Connected)
            await Connect(cancellationToken).ConfigureAwait(false);

        // The typed SendRequestAsync drops the content of a plain Response, which carries the reason a handler failed.
        var response = await _pipeClient.SendRequestAsync(request.Topic, content, cancellationToken: cancellationToken).ConfigureAwait(false);

        return PipeResponse.Deserialize(request, response.Content);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _pipeClient.DisposeAsync();
}
