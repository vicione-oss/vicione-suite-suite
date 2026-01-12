using Sdk.Messaging;

namespace Core.Shared.Modules.Events;

[ForwardToUI]
public sealed record ModuleManifestUpdatedEvent(Guid InstanceId, bool Success) : IEvent;
