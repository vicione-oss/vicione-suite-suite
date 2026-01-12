namespace Core.Shared.Instance.HealthCheck;

public interface IMasterHealthInfo
{
    bool IsMasterReachable { get; }
}
