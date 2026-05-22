using Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;
using Blazor.Shared.Services;
using Core.Shared.HostManagement;
using HostManagement.Shared.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Extensions;

internal static class RemoteAccessControlPanelStateExtensions
{
    public static void Initialize(this RemoteAccessControlPanelState state,
        ISystemConfigurationService systemConfigurationService,
        IConfiguration configuration,
        IOptions<HostManagementOptions> hostManagementOptions)
    {
        var sshServiceDetail = systemConfigurationService.SystemConfiguration.Services.FirstOrDefault(s => s.Name == hostManagementOptions.Value.SshServiceName);
        var moneoRcServiceDetailName = configuration[Constants.MoneoRcServiceNameConfigKey];
        var moneoRcServiceDetail = systemConfigurationService.SystemConfiguration.Services.FirstOrDefault(s => s.Name == moneoRcServiceDetailName);

        state.Terminal.CanSecureShell = sshServiceDetail is not null;
        state.Terminal.IsSecureShell = sshServiceDetail?.State == ServiceState.Enabled;

        state.Terminal.CanMoneoRc = moneoRcServiceDetail is not null;
        state.Terminal.IsMoneoRc = moneoRcServiceDetail?.State == ServiceState.Enabled;

        // Store initial values to detect changes during save
        state.IsSecureShellInitial = state.Terminal.IsSecureShell;
        state.IsMoneoRcInitial = state.Terminal.IsMoneoRc;
    }
}
