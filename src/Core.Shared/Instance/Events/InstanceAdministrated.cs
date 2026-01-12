using Core.Shared.Instance.Commands;
using Sdk.Messaging;

namespace Core.Shared.Instance.Events;

[ForwardToUI]
public sealed record InstanceAdministrated(Guid InstanceId, AdministrateInstanceAction Action, bool Success) : IEvent;
