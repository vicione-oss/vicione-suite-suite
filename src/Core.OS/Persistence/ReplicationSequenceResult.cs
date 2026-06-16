namespace Core.OS.Persistence;

public readonly struct ReplicationSequenceResult
{
    public static readonly ReplicationSequenceResult None = new([], false, false);
    public static readonly ReplicationSequenceResult Buffered = new([], false, true);

    public IReadOnlyList<DbChangeSet> MessagesToApply { get; }
    public bool RequiresFullSync { get; }
    public bool WasBuffered { get; }

    private ReplicationSequenceResult(IReadOnlyList<DbChangeSet> messagesToApply, bool requiresFullSync, bool wasBuffered)
    {
        MessagesToApply = messagesToApply;
        RequiresFullSync = requiresFullSync;
        WasBuffered = wasBuffered;
    }

    public static ReplicationSequenceResult Apply(IReadOnlyList<DbChangeSet> messages) => new(messages, false, false);
    public static ReplicationSequenceResult ApplyAndResync(IReadOnlyList<DbChangeSet> messages) => new(messages, true, false);
}
