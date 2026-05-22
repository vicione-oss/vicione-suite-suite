using Blazor.Shared.Network.ControlPanels.Proxies.Models;
using Blazor.Shared.Network.ControlPanels.Proxies.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts.Network;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Extensions;

internal static class ProxiesControlPanelStateExtensions
{
    public static void Initialize(this ProxiesControlPanelState state, ISystemConfigurationService systemConfigurationService)
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
