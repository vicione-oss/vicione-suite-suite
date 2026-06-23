namespace Core.OS.Instance.Contracts;

internal class RecoveryState
{
    public DateTimeOffset LastStartup { get; set; }

    public int Startups { get; set; }

    /// <summary>
    /// Indicates that recovery mode was previously applied during this crash cycle.
    /// If the suite still crashes after recovery, escalation to a terminal state is triggered.
    /// </summary>
    public bool RecoveryApplied { get; set; }
}
