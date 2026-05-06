using HostManagement.Shared.Contracts;

namespace Core.Shared.HostManagement.Services;

public interface ISystemConfigurationService
{
    SystemConfiguration SystemConfiguration { get; }

    DateTimeOffset? LastDhcpLeaseFetchUtc { get; }

    event Func<CancellationToken, Task>? SystemConfigurationChanged;

    Task Initialize(CancellationToken cancellationToken);

    Task SetSystemConfiguration(SystemConfiguration newSystemConfiguration, CancellationToken cancellationToken);
}
