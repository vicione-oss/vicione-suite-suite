using Core.Shared.HostManagement;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;

internal sealed class RemoteAccessControlPanelResetHandler(ISystemConfigurationService systemConfigurationService, IOptions<HostManagementOptions> hostMgmtOptions, IConfiguration config)
    : IControlPanelResetHandler<RemoteAccessControlPanelState>
{
    public Task Reset(RemoteAccessControlPanelState state, CancellationToken cancellationToken)
    {
        var sshServiceDetail = systemConfigurationService.SystemConfiguration.Services.FirstOrDefault(s => s.Name == hostMgmtOptions.Value.SshServiceName);
        var moneoRcServiceDetailName = config[Constants.MoneoRcServiceNameConfigKey];
        var moneoRcServiceDetail = systemConfigurationService.SystemConfiguration.Services.FirstOrDefault(s => s.Name == moneoRcServiceDetailName);

        state.Terminal.CanSecureShell = sshServiceDetail is not null;
        state.Terminal.IsSecureShell = sshServiceDetail?.State == ServiceState.Enabled;

        state.Terminal.CanMoneoRc = moneoRcServiceDetail is not null;
        state.Terminal.IsMoneoRc = moneoRcServiceDetail?.State == ServiceState.Enabled;

        // we keep the init value
        state.IsSecureShellInitial = state.Terminal.IsSecureShell;
        state.IsMoneoRcInitial = state.Terminal.IsMoneoRc;

        return Task.CompletedTask;
    }
}
