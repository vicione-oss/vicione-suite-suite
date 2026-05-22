using Blazor.Shared.Network.ControlPanels.RemoteAccess.Extensions;
using Blazor.Shared.Services;
using Core.Shared.HostManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;

internal sealed class RemoteAccessControlPanelResetHandler(ISystemConfigurationService systemConfigurationService, IOptions<HostManagementOptions> hostManagementOptions, IConfiguration configuration)
    : IControlPanelResetHandler<RemoteAccessControlPanelState>
{
    public Task Reset(RemoteAccessControlPanelState state, CancellationToken cancellationToken)
    {
        state.Initialize(systemConfigurationService, configuration, hostManagementOptions);

        return Task.CompletedTask;
    }
}
