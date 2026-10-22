using Core.OS.Instance;
using Core.Shared.Messaging;
using MassTransit;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Instance;

namespace Core.OS.MessageBus.MassTransit;

public sealed class EventForwardToUiConsumerDefinition<T> : ConsumerDefinition<EventForwardToUiConsumer<T>> where T : class
{
    internal const string QueueNamePrefix = "ForwardToUi";

    public EventForwardToUiConsumerDefinition(ILocalInstanceInformationProvider instanceInformationProvider,
        IOptions<InstanceOptions> instanceOptions)
    {
        EndpointName = GetEndpointName(instanceOptions.Value.Type, instanceInformationProvider.ReadLocalInstanceId());
    }

    /// <remarks>
    /// A standalone queue consumes one message at a time, so all forwarded types share a queue of their own there instead
    /// of waiting behind long instance-dependent commands such as a deployment. Forwarding only reads, in its own scope.
    /// The prefix avoids the instance queue's: this queue is not serialized with replication and takes the default ladder.
    /// Master and slave keep the instance queue, whose broker bindings would otherwise copy these events to <c>_skipped</c>;
    /// the standalone branch assumes the in-memory transport; on RabbitMQ the copies go on until the vhost is cleaned.
    /// </remarks>
    private static string GetEndpointName(InstanceType instanceType, Guid instanceId)
        => instanceType == InstanceType.Standalone
            ? $"{QueueNamePrefix}_{instanceId}"
            : $"{MessagingHelper.InstanceQueueNamePrefix}_{instanceId}";
}

public sealed partial class EventForwardToUiConsumer<T>(IEnumerable<IUiEventPublisher<T>> publisherList, ILogger<EventForwardToUiConsumer<T>> logger) :
    IConsumer<T> where T : class
{
    public async Task Consume(ConsumeContext<T> context)
    {
        var messageType = context.Message.GetType();
        if (context.TryGetPayload<MessageContext>(out var messageContext))
        {
            foreach (var publisher in publisherList)
            {
                // An event that does not implement CorrelatedBy<Guid?> loses its CorrelationId when published.
                // inside an activity because ExecuteContext is different
                try
                {
                    await publisher.PublishUiEvent(context.Message,
                        messageContext?.CorrelationId,
                        context.CancellationToken);
                }
                catch (Exception e)
                {
                    LogForwardToUiFailed(logger, e, messageType.Name, publisher.GetType().Name);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error forwarding a {MessageName}-Event to the UI using {PublisherName}")]
    private static partial void LogForwardToUiFailed(ILogger logger, Exception exception, string messageName, string publisherName);
}
