using Sdk.Messaging;

namespace Core.Shared.Persistence.Events;

[ForwardToUI]
public record BackupFinished(Guid CorrelationId, string? BackupFileName, ErrorInfo? ErrorInfo = null) : IEvent;

