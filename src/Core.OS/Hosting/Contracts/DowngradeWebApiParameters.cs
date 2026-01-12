using Core.OS.Instance;
using Core.Shared.HostManagement;

namespace Core.OS.Hosting.Contracts;

internal record DowngradeWebApiParameters
{
    public required InstanceOptions Instance { get; init; }

    public required HostManagementOptions HostManagement { get; init; }

    public required Serilog.ILogger Logger { get; init; }

    public string SuiteVersion { get; init; } = string.Empty;

    public string PersistedVersion { get; init; } = string.Empty;

    public int StopApplicationDelayMs { get; init; } = 2000;
}
