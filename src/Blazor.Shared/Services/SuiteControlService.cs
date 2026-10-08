using System.Globalization;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Requests;
using Core.Shared.Instance.Commands;
using HostManagement.Shared.Capabilities;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Instance;

namespace Blazor.Shared.Services;

internal sealed class SuiteControlService(IUiMediator mediator,
    IInstanceInformationProvider informationProvider,
    IMessageBannerService bannerService,
    ILogger<SuiteControlService> logger) : ISuiteControlService
{
    // We are currently unable to trigger a complete shutdown. This method shuts down the suite and then triggers a restart.
    public async Task RestartInstance()
    {
        if (!await EnsureEnabled(capabilities => capabilities.RestartService))
            return;

#if DEBUG
        var delay = 5;
#else
        var delay = 10;
#endif 
        var message = string.Format(CultureInfo.InvariantCulture, Localization.RestartSuite.SystemRestartInXSeconds, delay);
        bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Information, message);

        var command = new ControlInstance
        {
            InstanceId = informationProvider.Local.Id,
            Action = InstanceCommand.Restart,
            Delay = TimeSpan.FromSeconds(delay),
            CorrelationId = Guid.NewGuid()
        };

        logger.LogInformation("Requesting application restart for instance {InstanceId}", informationProvider.Local.Id);

        await mediator.Send(command, informationProvider.Local.Id);
    }

    public async Task RestartAllInstances()
    {
        // Only the local capability is checked. Other nodes refuse on their own, without feedback here, see #2927.
        if (!await EnsureEnabled(capabilities => capabilities.RestartService))
            return;

#if DEBUG
        var delay = 5;
#else
        var delay = 10;
#endif
        var message = string.Format(CultureInfo.InvariantCulture, Localization.RestartSuite.ClusterRestartInXSeconds, delay);
        bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Information, message);

        logger.LogInformation("Requesting cluster-wide restart");

        await mediator.Send(new RestartAllInstances { Delay = TimeSpan.FromSeconds(delay) });
    }

    public async Task RestartSystem()
    {
        if (!await EnsureEnabled(capabilities => capabilities.RestartSystem))
            return;

        logger.LogInformation("Requesting system restart for instance {InstanceId}", informationProvider.Local.Id);

        await mediator.Send(new ControlSystem(SystemCommand.Restart), informationProvider.Local.Id);
    }

    public async Task ShutdownSystem()
    {
        if (!await EnsureEnabled(capabilities => capabilities.ShutdownSystem))
            return;

        logger.LogInformation("Requesting system shutdown for instance {InstanceId}", informationProvider.Local.Id);

        await mediator.Send(new ControlSystem(SystemCommand.Shutdown), informationProvider.Local.Id);
    }

    /// <returns><see langword="false"/> after showing an error banner when HostManagement disables the capability.</returns>
    private async Task<bool> EnsureEnabled(Func<SystemControlCapabilities, CapabilityStatus> capability)
    {
        var response = await mediator.Request<GetSystemControlCapabilities, GetSystemControlCapabilitiesResponse>(new GetSystemControlCapabilities());
        if (response.Capabilities is null || capability(response.Capabilities) is not CapabilityStatus.Disabled)
            return true;

        bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Error, Localization.HostManagementCapabilities.FunctionDisabled);
        return false;
    }
}
