using HostManagement.Shared.Contracts;

namespace Core.Shared.HostManagement.Services;

public interface ISystemConfigurationService
{
    SystemConfiguration SystemConfiguration { get; }

    DateTimeOffset? LastDhcpLeaseFetchUtc { get; }

    event Func<Task>? SystemConfigurationChanged;

    Task Initialize(CancellationToken cancellationToken);

    Task SetSystemConfiguration(SystemConfiguration newSystemConfiguration);
}
