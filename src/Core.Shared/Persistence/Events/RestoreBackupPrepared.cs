using Sdk.Messaging;

namespace Core.Shared.Persistence.Events;

[ForwardToUI]
public record RestoreBackupPrepared(bool Restart, ErrorInfo? ErrorInfo = null) : IEvent;

