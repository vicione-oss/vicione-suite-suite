using System.Text.Json;
using MassTransit;
using MassTransit.Internals;
using Sdk.Messaging;

namespace Blazor.Wasm.Client.Infrastructure.SignalR;

public static class SignalRMessageFactory
{
    public static SignalRMessageEnvelope EnvelopResponse(IResponse message, Guid? correlationId)
        => EnvelopInternal(message, null, correlationId, null);

    public static SignalRMessageEnvelope Envelop(IRoutableMessage message,
        Guid? instanceId = null,
        Guid? correlationId = null,
        string? sourceAddress = null)
        => EnvelopInternal(message, instanceId, correlationId, sourceAddress);

    public static object DeserializePayload(this SignalRMessageEnvelope envelope, out Type messageType)
    {
        messageType = MessageTypeCache.GetOrAdd(envelope.PayloadTypeFullName ?? string.Empty);
        try
        {
            var deserializedMessage = JsonSerializer.Deserialize(envelope.Payload.AsSpan(), messageType, DefaultJsonSerializerSettings.Default);
            return deserializedMessage ?? throw new InvalidOperationException(ErrorOnSerializer(messageType.Name, "deserialize"));
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(ErrorOnSerializer(messageType.Name, "deserialize"), e);
        }
    }

    private static SignalRMessageEnvelope EnvelopInternal(object message, Guid? instanceId, Guid? correlationId, string? sourceAddress)
    {
        var messageType = message.GetType();

        try
        {
            var serializedMessage = JsonSerializer.Serialize(message, messageType, DefaultJsonSerializerSettings.Default);
            return new SignalRMessageEnvelope(messageType.FullName, serializedMessage, instanceId, sourceAddress)
            {
                CorrelationId = correlationId ?? GetCorrelationId(messageType, message),
            };
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(ErrorOnSerializer(messageType.Name, "serialize"), e);
        }
    }

    private static string ErrorOnSerializer(string messageTypeName, string operation)
        => $"Could not {operation} type {messageTypeName} with default serialization";

    /// <summary>
    /// we try to take correlation id from message object to provide it in the context (MassTransit does it that way)  
    /// </summary>
    /// <param name="messageType"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private static Guid? GetCorrelationId(Type messageType, object message)
    {
        var correlationId = GetGuidProperty(messageType, message, nameof(CorrelatedBy<Guid>.CorrelationId));
        if (correlationId is not null)
            return correlationId.Value;

        var eventId = GetGuidProperty(messageType, message, "EventId");

        if (eventId is null && !messageType.HasInterface<IEvent>())
            return Guid.NewGuid();
        return eventId;
    }

    private static Guid? GetGuidProperty(Type messageType, object message, string propertyName)
    {
        var correlationIdProperty = messageType.GetProperty(propertyName);

        var value = correlationIdProperty?.GetValue(message);
        if (value is not Guid guid)
            return null;
        return guid;
    }
}
