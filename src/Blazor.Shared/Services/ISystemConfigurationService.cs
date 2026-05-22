using HostManagement.Shared.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.SystemConfiguration.Events;

namespace Blazor.Shared.Services;

public interface ISystemConfigurationService
{
    SystemConfiguration SystemConfiguration { get; }

    DateTimeOffset? LastDhcpLeaseFetchUtc { get; }

    event Func<CancellationToken, Task>? SystemConfigurationChanged;

    Task Initialize(CancellationToken cancellationToken);

    Task SetSystemConfiguration(SystemConfiguration newSystemConfiguration, CancellationToken cancellationToken);
}
