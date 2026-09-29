namespace Core.OS.Persistence;

/// <summary>
/// Refreshes node-local state derived from replicated data once that data has been written on a slave.
/// </summary>
/// <remarks>
/// An event published alongside a change can reach a slave before the change set does, so state built from
/// replicated rows cannot rely on such an event alone.
/// </remarks>
internal interface IReplicationObserver
{
    /// <summary>
    /// Runs after a change set has been saved, with the full names of the entity types it applied.
    /// </summary>
    void ChangeSetApplied(string contextType, IReadOnlySet<string> entityTypeNames);

    /// <summary>
    /// Runs after a full sync, which rewrites tables with SQL and reports no entity types.
    /// </summary>
    void Resynchronized();
}
