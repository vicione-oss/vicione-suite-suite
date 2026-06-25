namespace Core.OS.Instance.Contracts;

/// <summary>
/// Outcome of the recovery mode analysis.
/// </summary>
internal enum RecoveryDecision
{
    /// <summary>No recovery action needed — continue normal startup.</summary>
    Continue,

    /// <summary>Recovery threshold reached — disable modules and retry.</summary>
    ApplyRecovery,

    /// <summary>Recovery was already applied and the suite still crashes — enter terminal failed state.</summary>
    RecoveryExhausted
}
