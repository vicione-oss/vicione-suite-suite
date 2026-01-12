using System.ComponentModel.DataAnnotations;

namespace Core.OS.Instance.HealthCheck;

public sealed class InstanceHealthCheckOptions
{
    [Range(0, int.MaxValue)]
    public int PublishDelayInSeconds { get; set; } = 30;

    [Range(0, int.MaxValue)]
    public int PublishIntervalInSeconds { get; set; } = 60;

    [Range(0, int.MaxValue)]
    public int MasterPublishIntervalInSeconds { get; set; } = 60;
}
