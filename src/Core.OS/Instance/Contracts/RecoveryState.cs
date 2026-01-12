namespace Core.OS.Instance.Contracts;

internal class RecoveryState
{
    public DateTime LastStartup { get; set; }

    public int Startups { get; set; }
}
