namespace Core.OS.Instance;

public class InstanceRecoveryOptions
{
    public int TimespanMinutes { get; set; } = 5;
    public int MaxStartupAttempts { get; set; } = 3;
}
