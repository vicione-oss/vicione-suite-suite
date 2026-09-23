using Core.OS.Instance;
using Core.Shared.Messaging;
using MassTransit;
using Sdk.Backend.Messaging;

namespace Core.OS.MessageBus.MassTransit;

public sealed class EventForwardToUiConsumerDefinition<T> : ConsumerDefinition<EventForwardToUiConsumer<T>> where T : class
{
    public EventForwardToUiConsumerDefinition(ILocalInstanceInformationProvider instanceInformationProvider)
    {
        EndpointName = $"{MessagingHelper.InstanceQueueNamePrefix}_{instanceInformationProvider.ReadLocalInstanceId()}";
    }
}

public sealed class EventForwardToUiConsumer<T>(IEnumerable<IUiEventPublisher<T>> publisherList, ILogger<EventForwardToUiConsumer<T>> logger) :
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
                    logger.LogWarning(e, "Error forwarding a {MessageName}-Event to the UI using {PublisherName}", messageType.Name, publisher.GetType().Name);
                }
            }
        }
    }
}
