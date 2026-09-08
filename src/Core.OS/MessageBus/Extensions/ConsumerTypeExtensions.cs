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
    /// A shim over an SDK defect, not a permanent abstraction: in the pinned SDK package
    /// <see cref="MessagingHelper.ConsumesRequest"/> is vacuously <see langword="true"/> for a type that reports no
    /// message types, which covers every <c>IConsumer&lt;Fault&lt;T&gt;&gt;</c> and every
    /// <c>ConsumerDefinition&lt;T&gt;</c>. Fixed in suite-sdk 3.1.0; this type and both of its call sites collapse
    /// onto the SDK helper once the package is bumped. See ADR-004 (D2).
    /// </remarks>
    public static bool ConsumesOnlyRequests(this Type consumerType)
        => consumerType.FindMessageTypes().Any() && consumerType.ConsumesRequest();
}
