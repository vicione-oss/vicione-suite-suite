using Core.OS.Instance;
using MassTransit;
using MassTransit.Internals;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.MessageBus.MassTransit;

internal sealed class SuiteEndpointNameFormatter(ILocalInstanceInformationProvider informationProvider) : IEndpointNameFormatter
{
    private readonly Lazy<Guid> _instanceId = new(informationProvider.ReadLocalInstanceId);

    string IEndpointNameFormatter.Separator => ".";

    public string TemporaryEndpoint(string tag) => DefaultEndpointNameFormatter.Instance.TemporaryEndpoint(tag);

    public string Consumer<T>()
        where T : class, IConsumer => GetConsumerName(typeof(T), _instanceId.Value);

    public string Message<T>()
        where T : class
    {
        var messageType = typeof(T);
        if (messageType.HasInterface<IRoutableMessage>())
            return GetRoute(messageType, _instanceId.Value);

        if (messageType.IsGenericType && messageType.Name.Contains('`', StringComparison.InvariantCultureIgnoreCase))
            return MessagingHelper.CleanName(messageType.GetGenericArguments().Last());

        return MessagingHelper.CleanName(messageType.Name);
    }

    public string Saga<T>()
        where T : class, ISaga => typeof(T).Name;

    public string ExecuteActivity<T, TArguments>()
        where T : class, IExecuteActivity<TArguments>
        where TArguments : class =>
        GetActivityName(typeof(T), _instanceId.Value);

    public string CompensateActivity<T, TLog>()
        where T : class, ICompensateActivity<TLog>
        where TLog : class =>
        GetActivityName(typeof(T), _instanceId.Value) + "_compensate";

    public string SanitizeName(string name) => name; // part of interface, but not used

    private static string GetActivityName(Type activityType, Guid? instanceId)
    {
        const string activity = "Activity";

        var activityName = MessagingHelper.CleanName(activityType.Name);

        if (activityName.EndsWith(activity, StringComparison.InvariantCultureIgnoreCase))
            activityName = activityName[..^activity.Length];

        return activityType.FindMessageTypes().SingleOrDefault() is not { } att
            ? $"__{activityName}"
            : $"{att.GetActivityEndpointName(instanceId)}";
    }

    private static string GetRoute(Type messageType, Guid? instanceId)
    {
        if (messageType.IsInstanceDependent() && instanceId is null)
            throw new ArgumentNullException(nameof(instanceId),
                $"{messageType.Name} is instance dependent, but no instance Id was provided when getting the route");
        var usedMessageName = messageType.GetEndpointName() ?? messageType.Name;
        return messageType.IsInstanceDependent()
            ? MessagingHelper.CleanName($"{usedMessageName}_{instanceId}")
            : MessagingHelper.CleanName(usedMessageName);
    }

    internal static string GetConsumerName(Type consumerType, Guid instanceId)
    {
        var messageTypes = consumerType.FindMessageTypes().ToList();
        if (messageTypes.Count == 0)
            return GetDefaultConsumerName(); // Consumer that is not routed
        if (messageTypes.Count > 1)
        {
            if (messageTypes.Any(r => r.GetInterface<IInstanceDependentMessage>() is not null))
                throw new InvalidOperationException(
                    $"{consumerType.Name} consumes multiple messages with at least one being instance-dependant. Consumers handling instance-dependant messages must to so exclusively");
            if (!messageTypes.All(r => r.HasInterface<IEvent>()))
                throw new InvalidOperationException($"{consumerType.Name} consumes multiple messages with at least one not being an event");
            return MessagingHelper.GetEventEndpointName(consumerType, messageTypes[0], instanceId);
        }

        // At this point we have exactly 1 Type
        var messageType = messageTypes[0];

        if (messageType.HasInterface<ICommand>() || messageType.HasInterface<IInstanceDependentCommand>())
            return MessagingHelper.GetCommandEndpointName(consumerType, messageType, instanceId);
        if (messageType.HasInterface<IEvent>() || messageType.HasInterface<IInstanceEvent>())
            return MessagingHelper.GetEventEndpointName(consumerType, messageType, instanceId);
        if (messageType.HasInterface(typeof(IRequest<>)) || messageType.HasInterface(typeof(IInstanceDependentRequest<>)))
            return MessagingHelper.GetRequestEndpointName(messageType, instanceId);

        var endpoint = messageType.GetEndpointName();
        if (endpoint is null)
            return MessagingHelper.CleanName(messageType.IsInstanceDependent() ? $"{GetDefaultConsumerName(true)}_{instanceId}" : GetDefaultConsumerName(true));

        return MessagingHelper.CleanName(messageType.IsInstanceDependent() ? $"{endpoint}_{instanceId}" : $"{endpoint}");

        // A full name is long, especially with the id appended, but it has to be unique.
        //Otherwise, two consumers with the same name would compete
        string GetDefaultConsumerName(bool fullName = false)
        {
            if (consumerType.IsGenericType && consumerType.Name.Contains('`', StringComparison.InvariantCultureIgnoreCase))
                return MessagingHelper.CleanName(consumerType.GetGenericArguments().Last(), fullName);

            const string consumer = "Consumer";

            var consumerName = MessagingHelper.CleanName(consumerType, fullName);

            if (consumerName.EndsWith(consumer, StringComparison.InvariantCultureIgnoreCase))
                consumerName = consumerName[..^consumer.Length];

            return consumerName;
        }
    }
}
