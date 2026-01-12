namespace Core.OS.Instance.Contracts;

internal class RecoveryState
{
    public DateTimeOffset LastStartup { get; set; }

    public int Startups { get; set; }
}
