using System.Globalization;
using Core.Shared.Instance.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Instance;

namespace Blazor.Shared.Services;

internal sealed class SuiteControlService(IUiMediator mediator, IInstanceInformationProvider informationProvider, IMessageBannerService bannerService) : ISuiteControlService
{
    // We are currently unable to trigger a complete shutdown. This method shuts down the suite and then triggers a restart.
    public async Task ShutdownSuite()
    {
#if DEBUG
        var delay = 5;
#else
        var delay = 10;
#endif 
        var message = string.Format(CultureInfo.InvariantCulture, Localization.RestartSuite.SystemRestartInXSeconds, delay);
        bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Information, message);

        await ShutdownCoreOS(TimeSpan.FromSeconds(delay));
    }

    private async Task ShutdownCoreOS(TimeSpan shutdownDelay, CancellationToken cancellationToken = default)
    {
        var command = new ShutdownInstance
        {
            InstanceId = informationProvider.Local.Id,
            Reason = "ModulesModified",
            Delay = shutdownDelay
        };
        await mediator.Send(command, informationProvider.Local.Id, cancellationToken);
    }
}
