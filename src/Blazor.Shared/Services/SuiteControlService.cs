using System.Globalization;
using Core.Shared.HostManagement.Commands;
using Core.Shared.Instance.Commands;
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
    public async Task RestartSuite()
    {
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

    public async Task RestartSystem()
    {
        logger.LogInformation("Requesting system restart for instance {InstanceId}", informationProvider.Local.Id);

        await mediator.Send(new ControlSystem(SystemCommand.Restart), informationProvider.Local.Id);
    }

    public async Task ShutdownSystem()
    {
        logger.LogInformation("Requesting system shutdown for instance {InstanceId}", informationProvider.Local.Id);

        await mediator.Send(new ControlSystem(SystemCommand.Shutdown), informationProvider.Local.Id);
    }
}
