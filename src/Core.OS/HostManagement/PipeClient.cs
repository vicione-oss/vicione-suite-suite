using System.IO.Pipes;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Contracts;
using HostManagement.Shared.Communication.Enums;
using Microsoft.Extensions.Options;

namespace Core.OS.HostManagement;

public sealed class PipeClient(IOptions<HostManagementOptions> options, ILogger<PipeClient> logger, EventCallbackRegistry eventCallbackRegistry) :
    IPipeClient
{
    private const int CommunicationDelayMs = 250; //Temporary workaround for HM re-creating the pipe after each call.
    private PipeMessageProcessor? _pipeMessageProcessor;
    private readonly PipeOptions? _pipeOptions = options.Value.PipeOptions;
    private readonly string _pipeName = options.Value.PipeName;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private NamedPipeClientStream? _pipeClientStream;
    private PipeStreamWrapper? _pipeStreamWrapper;

    private NamedPipeClientStream PipeClientStream => _pipeClientStream ??= CreatePipeClientStream();
    private PipeStreamWrapper PipeStreamWrapper => _pipeStreamWrapper ??= new PipeStreamWrapper(PipeClientStream, logger);
    private PipeMessageProcessor PipeMessageProcessor => _pipeMessageProcessor ??= new PipeMessageProcessor(PipeStreamWrapper, eventCallbackRegistry, logger);

    /// <inheritdoc/>
    public PipeState State => _pipeMessageProcessor?.State ?? PipeState.NotOpened;

    /// <inheritdoc/>
    public async Task Connect(CancellationToken cancellationToken = default)
    {
        await PipeClientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);

        logger.LogDebug("Connected to server");

        // Start the processing thread
        _ = PipeMessageProcessor.StartProcessing(cancellationToken);
    }

    public async Task<string> SendRequest(string topic, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(CommunicationDelayMs, cancellationToken);
            var completedResponse = await SendRequestInternal(topic, content, cancellationToken);

            return completedResponse;
        }
        finally
        {
            _pipeMessageProcessor?.Dispose();
            _pipeMessageProcessor = null;
            _pipeStreamWrapper?.Dispose();
            _pipeStreamWrapper = null;
            if (_pipeClientStream is not null)
            {
                await _pipeClientStream.DisposeAsync();
                _pipeClientStream = null;
            }

            if (_requestLock.CurrentCount < 1)
            {
                _requestLock.Release();
                logger.LogDebug("Sent request for topic='{Topic}' and released lock", topic);
            }
            else
            {
                logger.LogDebug("Sent request for topic='{Topic}'", topic);       
            }
        }
    }

    private async Task<string> SendRequestInternal(string topic, string content, CancellationToken cancellationToken = default)
    {
        // use cached response
        if (State != PipeState.Connected)
            await Connect(cancellationToken);

        var message = new NamedPipeMessage { Topic = topic, Type = MessageType.Request, Content = content };

        var expectedResponse = PipeMessageProcessor.AnnounceResponse(message.ConversationId);

        try
        {
            logger.LogDebug("Send hostmanagement request with topic='{Topic}' content.length={Length}", topic, content.Length);
            logger.LogTrace("Request content: {ConfigJson}", content);

            await PipeStreamWrapper.SendMessage(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send message with topic {Topic}", topic);
            throw;
        }

        cancellationToken.Register(
            () =>
            {
                expectedResponse.TaskCompletionSource.TrySetException(new OperationCanceledException("Request has been canceled."));
            },
            false);

        return await expectedResponse.TaskCompletionSource.Task.ConfigureAwait(false);
    }

    private NamedPipeClientStream CreatePipeClientStream()
    {
        var pipeOptions = _pipeOptions == null
            ? PipeOptions.Asynchronous
            : _pipeOptions.Value | PipeOptions.Asynchronous;

        logger.LogDebug("Connecting to pipe \'{PipeName}\'", _pipeName);

        return new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, pipeOptions);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _pipeMessageProcessor?.Dispose();
        _pipeStreamWrapper?.Dispose();
        _pipeClientStream?.Dispose();
        _requestLock.Dispose();
    }
}
