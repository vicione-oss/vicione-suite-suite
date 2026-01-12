using Sdk.Messaging;

namespace Core.Shared.Modules.Events;

[ForwardToUI]
public sealed record ModuleOptionsUpdatedEvent(string ModuleId, bool Success) : IEvent;
