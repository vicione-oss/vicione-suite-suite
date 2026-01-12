namespace Blazor.Wasm.Client.Infrastructure.SignalR;

public sealed record SignalRMessageEnvelope(string? PayloadTypeFullName, string Payload, Guid? InstanceId = null, string? SourceAddress = null)
{
    public Guid? CorrelationId { get; init; }

    /// <summary>
    /// signalR client address
    /// </summary>
    public string? SourceAddress { get; init; } = SourceAddress;

    /// <summary>
    /// the payload type name without namespace 
    /// </summary>
    public string? PayloadTypeFullName { get; } = PayloadTypeFullName;

    /// <summary>
    /// the serialized transported message
    /// </summary>
    public string Payload { get; } = Payload;

    public bool Failed { get; init; }
}
