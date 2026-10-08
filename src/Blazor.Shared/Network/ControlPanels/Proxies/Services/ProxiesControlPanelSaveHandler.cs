using System.Globalization;
using Blazor.Shared.Network.ControlPanels.Proxies.Models;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Network.Models;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Services;

internal sealed class ProxiesControlPanelSaveHandler(IUiMediator mediator, ISystemConfigurationService systemConfigurationService, ILogger<ProxiesControlPanelSaveHandler> logger)
    : NetworkControlPanelSaveHandlerBase<ProxiesControlPanelState>(mediator, systemConfigurationService, logger)
{
    protected override Task<ISaveInternalResult> SaveInternal(ProxiesControlPanelState state)
    {
        var networkProxySettings = new NetworkProxySettings();

        if (SaveProxySettings(state.HttpProxySettings, networkProxySettings.HTTP) is SaveErrorResult httpProxySaveErrorResult)
            return Task.FromResult<ISaveInternalResult>(new SaveInternalErrorResult(httpProxySaveErrorResult.Message, httpProxySaveErrorResult.ErrorCode));

        if (SaveProxySettings(state.HttpsProxySettings, networkProxySettings.HTTPS) is SaveErrorResult httpsProxySaveErrorResult)
            return Task.FromResult<ISaveInternalResult>(new SaveInternalErrorResult(httpsProxySaveErrorResult.Message, httpsProxySaveErrorResult.ErrorCode));

        if (SaveProxySettings(state.FtpProxySettings, networkProxySettings.FTP) is SaveErrorResult ftpProxySaveErrorResult)
            return Task.FromResult<ISaveInternalResult>(new SaveInternalErrorResult(ftpProxySaveErrorResult.Message, ftpProxySaveErrorResult.ErrorCode));

        if (SaveProxySettings(state.SftpProxySettings, networkProxySettings.SFTP) is SaveErrorResult sftpProxySaveErrorResult)
            return Task.FromResult<ISaveInternalResult>(new SaveInternalErrorResult(sftpProxySaveErrorResult.Message, sftpProxySaveErrorResult.ErrorCode));

        // Proxy settings are not applied.
        {
            networkProxySettings.NoProxy.Enabled = state.DoNotProxyListEnabled;

            // Unfilled fieldsets and duplicates are dropped.
            state.DoNotProxyDetails = [.. state.DoNotProxyDetails.Where(d => !string.IsNullOrWhiteSpace(d.HostnameOrIp)).Distinct()];

            networkProxySettings.NoProxy.Entries.Clear();
            networkProxySettings.NoProxy.Entries.AddRange(state.DoNotProxyDetails.Select(d => d.HostnameOrIp));

            state.DoNotProxyDetails.EnsureAtLeastOneItemExists();
        }

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfaces = SystemConfigurationService.SystemConfiguration.NetworkInterfaces,
            NetworkDNSSettings = SystemConfigurationService.SystemConfiguration.NetworkDNSSettings,
            NetworkProxySettings = networkProxySettings,
            NetworkNTPSettings = SystemConfigurationService.SystemConfiguration.NetworkNTPSettings,
            Services = SystemConfigurationService.SystemConfiguration.Services
        };

        return Task.FromResult<ISaveInternalResult>(new SystemConfigurationSaveInternalResult(systemConfiguration));
    }

    private static ISaveResult SaveProxySettings(ProxySettings proxySettings, NetworkProxyDetail proxyDetail)
    {
        proxyDetail.Enabled = proxySettings.Enabled;
        proxyDetail.Server = proxySettings.Server;

        if (int.TryParse(proxySettings.Port, out var port))
        {
            proxyDetail.Port = port;
        }
        else
        {
            if (proxyDetail.Enabled)
                return new SaveErrorResult(string.Format(CultureInfo.CurrentCulture, Localization.ProxiesControlPanelSaveHandler.PortIsNotAnUnsignedInteger, proxySettings.Port));
        }

        if (proxySettings.PasswordRequired)
        {
            proxyDetail.Username = proxySettings.Username;
            proxyDetail.Password = proxySettings.Password;
        }
        else
        {
            proxyDetail.Username = null;
            proxyDetail.Password = null;
        }

        return new SaveSuccessResult();
    }
}
