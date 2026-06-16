using Sdk.Messaging;

namespace Core.OS.Persistence;

[MessageEndpoint("DbReplication")]
public sealed record DbChangeSet(List<ChangedEntity> Changes, string ContextType, long SequenceNumber, DateTimeOffset PublishedAt) : IInstanceEvent;
