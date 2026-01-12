using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;

namespace Core.Shared.HostManagement.Services;

public interface ISystemConfigurationService
{
    SystemConfiguration SystemConfiguration { get; }

    DateTimeOffset? LastDhcpLeaseFetchUtc { get; }

    event Func<Task>? SystemConfigurationChanged;

    Task Initialize(CancellationToken cancellationToken);

    Task SetSystemConfiguration(SystemConfiguration newSystemConfiguration);

    Task SetDhcpLease(DHCPLease dhcpLease, string interfaceName);
}
