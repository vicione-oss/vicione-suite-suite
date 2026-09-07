using MassTransit.Internals;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.MessageBus.Extensions;

internal static class ConsumerTypeExtensions
{
    /// <summary>
    /// Determines whether a type consumes request messages and nothing else.
    /// </summary>
    /// <remarks>
    /// <see cref="MessagingHelper.ConsumesRequest"/> alone does not answer this. It is an <c>All</c> over
    /// <see cref="MessagingHelper.FindMessageTypes"/>, so it is vacuously <see langword="true"/> for a type that
    /// reports no message types at all — and two kinds of type do exactly that. A <c>ConsumerDefinition&lt;T&gt;</c>
    /// implements no <c>IConsumer&lt;&gt;</c> in the first place, and <c>FindMessageTypes</c> skips generic message
    /// types, so every <c>IConsumer&lt;Fault&lt;T&gt;&gt;</c> looks message-less too. Requiring at least one message
    /// type keeps both of them out.
    /// </remarks>
    public static bool ConsumesOnlyRequests(this Type consumerType)
        => consumerType.FindMessageTypes().Any() && consumerType.ConsumesRequest();
}
