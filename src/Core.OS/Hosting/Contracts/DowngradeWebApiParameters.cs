using Core.OS.Instance;
using Core.Shared.HostManagement;

namespace Core.OS.Hosting.Contracts;

internal record DowngradeWebApiParameters
{
    public required InstanceOptions Instance { get; init; }

    public required HostManagementOptions HostManagement { get; init; }

    public required ILogger Logger { get; init; }

    public required VersionDowngradeInformation DowngradeInformation { get; init; }

    public int StopApplicationDelayMs { get; init; } = 2000;
}
