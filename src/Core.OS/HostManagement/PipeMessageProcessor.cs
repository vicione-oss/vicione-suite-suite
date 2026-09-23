using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;

namespace Core.OS.HostManagement;

/// <summary>
/// Handles the pipe connection state and work processing.
/// </summary>
internal sealed class PipeMessageProcessor(
    PipeStreamWrapper pipeStreamWrapper,
    EventCallbackRegistry eventCallbackRegistry,
    ILogger logger)
    : MessageProcessorBase(pipeStreamWrapper)
{
    private readonly Dictionary<Guid, ExpectedResponse> _expectedResponses = [];

    public ExpectedResponse AnnounceResponse(Guid conversationId)
    {
        var response = new ExpectedResponse();
        _expectedResponses.Add(conversationId, response);
        return response;
    }

    /// <summary>
    /// Processes the next message on the input stream.
    /// </summary>
    protected override async Task ProcessMessage(CancellationToken cancellationToken)
    {
        var message = await PipeStreamWrapper.ReadMessage(cancellationToken).ConfigureAwait(false);

        switch (message.Type)
        {
            case MessageType.Request:
                throw new InvalidOperationException($"Message type '{message.Type}' may only be send by the server.");
            case MessageType.Response:
                if (!_expectedResponses.Remove(message.ConversationId, out var expectedResponse))
                {
                    logger.LogWarning("Unexpected response on conversation {ConversationId} with topic {Topic}", message.ConversationId, message.Topic);
                    // TODO: throw?
                    return;
                }
                expectedResponse.TaskCompletionSource.TrySetResult(message.Content);
                break;
            case MessageType.Event:
                await eventCallbackRegistry.Handle(message, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unrecognized message type: {message.Type}");
        }
    }
}
