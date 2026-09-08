using System.Collections.Frozen;
using System.Reflection;
using Core.OS.MessageBus.Extensions;
using MassTransit.Metadata;
using MassTransit.Util;
using Sdk.Backend.Messaging;

namespace Core.OS.MessageBus.MassTransit.Configuration;

/// <summary>
/// Maps receive endpoint (queue) names to the retry ladder they get. See ADR-004 (D2).
/// </summary>
internal static class MessageRetryClassifier
{
    /// <summary>
    /// Default ladder for <see cref="MessageRetryClass.Default"/>, roughly 6 seconds. See ADR-004 (D2). Kept as short
    /// as the instance queue's, because <c>Commands</c> and <c>Events</c> are shared, serialized queues too.
    /// </summary>
    public static readonly int[] DefaultIntervals = [200, 1000, 5000];

    /// <summary>
    /// Default ladder for <see cref="MessageRetryClass.SerializedInstance"/>, roughly 6 seconds. See ADR-004 (D2).
    /// </summary>
    public static readonly int[] SerializedInstanceIntervals = [200, 1000, 5000];

    /// <summary>
    /// Default ladder for <see cref="MessageRetryClass.Request"/>, roughly 4 seconds. See ADR-004 (D2).
    /// </summary>
    public static readonly int[] RequestIntervals = [200, 1000, 3000];

    /// <summary>
    /// Collects the request endpoint names, which are the only ones that cannot be recognized from the queue name
    /// alone. Endpoints that are not found stay on the default ladder, which is short, so an unrecognized endpoint
    /// cannot stall a shared queue.
    /// </summary>
    /// <remarks>
    /// The scan is seeded with <see cref="RegistrationMetadata.IsConsumerOrDefinition"/>, so it also yields consumer
    /// definitions and consumers whose only message type is generic. Neither is a request consumer and both must be
    /// filtered out, or they take the shortest ladder in the table - see
    /// <see cref="ConsumerTypeExtensions.ConsumesOnlyRequests"/>.
    /// </remarks>
    public static FrozenSet<string> FindRequestEndpoints(Assembly[] assembliesToScan, Guid instanceId)
    {
        var types = AssemblyTypeCache.FindTypes(assembliesToScan, RegistrationMetadata.IsConsumerOrDefinition).GetAwaiter().GetResult();

        return types.FindTypes(TypeClassification.Concrete | TypeClassification.Closed)
            .Where(consumerType => consumerType.ConsumesOnlyRequests())
            .Select(consumerType => SuiteEndpointNameFormatter.GetConsumerName(consumerType, instanceId))
            .ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Picks the retry class of a receive endpoint from its queue name. See ADR-004 (D2).
    /// </summary>
    /// <remarks>
    /// The instance queue is tested first and wins over every other class. It is shared by replication and by every
    /// instance-dependent consumer, including instance-dependent *requests* — <c>GetEnvironmentOverridesConsumer</c>
    /// resolves to this very queue. Those stay out of <paramref name="requestEndpoints"/> today only because
    /// <c>IInstanceDependentRequest&lt;T&gt;</c> does not extend <c>IRequest&lt;T&gt;</c>; were that ever changed, a
    /// request-first order would silently drop the whole serialized queue — replication included — onto the shortest
    /// ladder. The queue's serialization property decides its ladder, never a single consumer sitting on it.
    /// </remarks>
    public static MessageRetryClass Classify(string queueName, FrozenSet<string> requestEndpoints)
    {
        if (queueName.StartsWith(MessagingHelper.InstanceQueueNamePrefix, StringComparison.Ordinal))
            return MessageRetryClass.SerializedInstance;

        return requestEndpoints.Contains(queueName)
            ? MessageRetryClass.Request
            : MessageRetryClass.Default;
    }

    /// <summary>
    /// Returns the configured ladder of the given class. An unconfigured (<see langword="null"/>) ladder falls back to
    /// the default for that class; an explicitly empty one is the operator's "do not retry" and is returned as is.
    /// </summary>
    public static int[] GetRetryIntervals(MessageBusOptions options, MessageRetryClass retryClass)
        => retryClass switch
        {
            MessageRetryClass.SerializedInstance => options.InstanceQueueRetryIntervals ?? SerializedInstanceIntervals,
            MessageRetryClass.Request => options.RequestRetryIntervals ?? RequestIntervals,
            _ => options.RetryIntervals ?? DefaultIntervals
        };
}
