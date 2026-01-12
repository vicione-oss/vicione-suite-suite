using Blazor.Shared.Network.ControlPanels.Proxies.Models;
using Blazor.Shared.Network.Extensions;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Services;

internal sealed class ProxiesControlPanelResetHandler(ISystemConfigurationService systemConfigurationService) : IControlPanelResetHandler<ProxiesControlPanelState>
{
    public Task Reset(ProxiesControlPanelState state, CancellationToken cancellationToken)
    {
        var networkProxySettings = systemConfigurationService.SystemConfiguration.NetworkProxySettings;

        ResetProxySettings(state.HttpProxySettings, networkProxySettings.HTTP);
        ResetProxySettings(state.HttpsProxySettings, networkProxySettings.HTTPS);
        ResetProxySettings(state.SocksProxySettings, networkProxySettings.SOCKS);
        ResetProxySettings(state.FtpProxySettings, networkProxySettings.FTP);
        ResetProxySettings(state.SftpProxySettings, networkProxySettings.SFTP);

        // do not use proxy settings
        {
            state.DoNotProxyListEnabled = networkProxySettings.DoNotProxyListEnabled;

            state.DoNotProxyDetails = [.. networkProxySettings.DoNotProxyList.Select(d => new DoNotProxyDetail { HostnameOrIp = d }).Distinct()];

            state.DoNotProxyDetails.EnsureAtLeastOneItemExists();
        }

        return Task.CompletedTask;
    }

    private static void ResetProxySettings(ProxySettings proxySettings, NetworkProxyDetail proxyDetail)
    {
        proxySettings.Enabled = proxyDetail.Enabled;
        proxySettings.Server = proxyDetail.Server;
        proxySettings.Port = $"{proxyDetail.Port}";
        proxySettings.PasswordRequired = !string.IsNullOrWhiteSpace(proxyDetail.Username) || !string.IsNullOrWhiteSpace(proxyDetail.Password);
        proxySettings.Username = proxyDetail.Username;
        proxySettings.Password = proxyDetail.Password;
    }
}
